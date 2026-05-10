using System;
using System.Collections.Generic;
using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AIT_App.Services;

namespace AIT_App
{
    // Раздел "Отчёт по сессии".
    // Показывает экзаменационные оценки в виде pivot-таблицы:
    //   строки = студенты, столбцы = предметы, ячейки = оценки.
    // Вторая таблица — статистика успеваемости по предметам.
    public partial class SessionReport : UserControl
    {
        private DataBaseCon _db = new DataBaseCon();

        // Сохраняем последние данные для экспорта
        private DataTable _lastGrades = null;
        private DataTable _lastPerf = null;

        public SessionReport()
        {
            InitializeComponent();

            BtnShow.Click += (s, e) => BuildReport();
            BtnExport.Click += OnExportClick;

            // Загружаем группы при открытии раздела
            LoadGroups();

            // Ставим даты по умолчанию: последний месяц
            DateFromPicker.SelectedDate = DateTime.Today.AddMonths(-1);
            DateToPicker.SelectedDate = DateTime.Today;
        }

        private void LoadGroups()
        {
            string sql = "SELECT `Название группы` FROM `Группы` ORDER BY `Название группы`";
            var table = _db.ExecuteQuery(sql);

            var groups = new List<string>();
            if (table != null)
                foreach (DataRow row in table.Rows)
                    groups.Add(row["Название группы"].ToString());

            GroupCombo.ItemsSource = groups;
        }

        // Строит оба отчёта по выбранным фильтрам
        private void BuildReport()
        {
            string group = GroupCombo.SelectedItem as string;
            var from = DateFromPicker.SelectedDate?.Date;
            var to = DateToPicker.SelectedDate?.Date;

            if (string.IsNullOrEmpty(group) || from == null || to == null)
            {
                _ = Dialogs.WarnAsync("Отчёт", "Выберите группу и диапазон дат.");
                return;
            }

            if (from > to)
            {
                _ = Dialogs.WarnAsync("Отчёт", "Дата «с» не может быть позже даты «по».");
                return;
            }

            // Загружаем все экзаменационные оценки для выбранной группы и периода
            string sql = @"
                SELECT
                    u.`ФИО`,
                    a.`Предмет`,
                    a.`Оценка`,
                    a.`Дата`
                FROM `Аттестация` a
                INNER JOIN `Ученики` u ON a.`Номер студента` = u.`ID`
                WHERE u.`Группа` = @group
                  AND a.`Тип` = 'Экзаменационная'
                  AND a.`Дата` BETWEEN @from AND @to
                ORDER BY u.`ФИО`, a.`Предмет`";

            var flat = _db.ExecuteQuery(sql, new Dictionary<string, object>
            {
                { "group", group },
                { "from", from },
                { "to", to }
            });

            if (flat == null)
            {
                _ = Dialogs.ErrorAsync("Отчёт", "Не удалось загрузить данные из базы.");
                return;
            }

            if (flat.Rows.Count == 0)
            {
                GradesEmpty.IsVisible = true;
                GradesGrid.Columns.Clear();
                GradesGrid.ItemsSource = null;
                PerfGrid.ItemsSource = null;
                _lastGrades = null;
                _lastPerf = null;
                return;
            }

            GradesEmpty.IsVisible = false;

            // Строим pivot-таблицу из плоских данных
            _lastGrades = BuildPivot(flat);
            _lastPerf = BuildPerformance(flat);

            // Генерируем колонки GradesGrid вручную:
            //   ФИО — обычная текстовая колонка;
            //   остальные (предметы) — DataGridTemplateColumn с цветным бейджем оценки.
            GradesGrid.Columns.Clear();
            foreach (DataColumn col in _lastGrades.Columns)
            {
                if (col.ColumnName == "ФИО")
                {
                    GradesGrid.Columns.Add(new DataGridTextColumn
                    {
                        Header = col.Caption,
                        Binding = new Binding($"[{col.ColumnName}]"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    });
                }
                else
                {
                    GradesGrid.Columns.Add(new DataGridTemplateColumn
                    {
                        Header = col.Caption,
                        Width = new DataGridLength(110),
                        CellTemplate = BuildGradeCellTemplate(col.ColumnName)
                    });
                }
            }

            GradesGrid.ItemsSource = DataBaseCon.ToRowList(_lastGrades);
            PerfGrid.ItemsSource = DataBaseCon.ToRowList(_lastPerf);
        }

        // Создаёт шаблон ячейки с цветным бейджем оценки.
        // columnName — имя колонки в DataTable (например "Col0"), к которому биндимся.
        private FuncDataTemplate<object> BuildGradeCellTemplate(string columnName)
        {
            // Конвертеры stateless — создаём свежие экземпляры для каждой ячейки.
            // Они сами берут кисти из ресурсов приложения.
            var bgConv = new GradeBackgroundConverter();
            var fgConv = new GradeForegroundConverter();

            return new FuncDataTemplate<object>((row, _) =>
            {
                var border = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 3),
                    MinWidth = 36
                };

                var text = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 13
                };

                // Биндим текст ячейки на нужную колонку DataRow
                text.Bind(TextBlock.TextProperty,
                    new Binding($"[{columnName}]"));

                // Биндим цвета через конвертеры
                border.Bind(Border.BackgroundProperty,
                    new Binding($"[{columnName}]") { Converter = bgConv });
                text.Bind(TextBlock.ForegroundProperty,
                    new Binding($"[{columnName}]") { Converter = fgConv });

                border.Child = text;
                return border;
            });
        }

        // Преобразует плоскую таблицу в pivot: строки = студенты, столбцы = предметы
        private DataTable BuildPivot(DataTable flat)
        {
            // Собираем уникальных студентов и предметы
            var students = new List<string>();
            var subjects = new SortedSet<string>();

            foreach (DataRow row in flat.Rows)
            {
                string student = row["ФИО"].ToString();
                string subject = row["Предмет"].ToString();

                if (!students.Contains(student))
                    students.Add(student);
                subjects.Add(subject);
            }

            // Создаём результирующую таблицу: первая колонка ФИО, остальные — предметы.
            // Колонки именуются Col0, Col1... (Caption = название предмета) —
            // чтобы Avalonia binding не падал на пробелах в названиях предметов.
            var pivot = new DataTable();
            pivot.Columns.Add("ФИО", typeof(string));
            int colIdx = 0;
            foreach (string subject in subjects)
                pivot.Columns.Add($"Col{colIdx++}", typeof(string)).Caption = subject;

            // Заполняем словарь (студент, предмет) -> оценка
            var grades = new Dictionary<string, Dictionary<string, string>>();
            foreach (DataRow row in flat.Rows)
            {
                string student = row["ФИО"].ToString();
                string subject = row["Предмет"].ToString();
                string grade = row["Оценка"].ToString();

                if (!grades.ContainsKey(student))
                    grades[student] = new Dictionary<string, string>();
                grades[student][subject] = grade;
            }

            // Добавляем строки в pivot; колонки обходим по Caption = название предмета
            students.Sort();
            foreach (string student in students)
            {
                DataRow pivotRow = pivot.NewRow();
                pivotRow["ФИО"] = student;

                foreach (DataColumn col in pivot.Columns)
                {
                    if (col.ColumnName == "ФИО") continue;
                    string subject = col.Caption;
                    pivotRow[col] = (grades.ContainsKey(student) && grades[student].ContainsKey(subject))
                        ? grades[student][subject]
                        : "";
                }

                pivot.Rows.Add(pivotRow);
            }

            return pivot;
        }

        // Строит таблицу статистики успеваемости по каждому предмету
        private DataTable BuildPerformance(DataTable flat)
        {
            var perf = new DataTable();
            perf.Columns.Add("Предмет", typeof(string));
            perf.Columns.Add("СреднийБалл", typeof(double)).Caption = "Средний балл";
            perf.Columns.Add("КачествоПроц", typeof(double)).Caption = "Качество % (4 и 5)";
            perf.Columns.Add("УспеваемостьПроц", typeof(double)).Caption = "Успеваемость % (3,4,5)";
            perf.Columns.Add("ВсегоОценок", typeof(int)).Caption = "Всего оценок";

            // Группируем оценки по предметам
            var bySubject = new Dictionary<string, List<string>>();
            foreach (DataRow row in flat.Rows)
            {
                string subject = row["Предмет"].ToString();
                string grade = row["Оценка"].ToString();

                if (!bySubject.ContainsKey(subject))
                    bySubject[subject] = new List<string>();
                bySubject[subject].Add(grade);
            }

            foreach (var kvp in bySubject)
            {
                string subject = kvp.Key;
                var gradeList = kvp.Value;
                int total = gradeList.Count;

                // Считаем суммарный балл для среднего (Н считаем как 0)
                double sum = 0;
                int quality = 0; // количество 4 и 5
                int pass = 0;    // количество 3, 4 и 5

                foreach (string g in gradeList)
                {
                    if (g == "5") { sum += 5; quality++; pass++; }
                    else if (g == "4") { sum += 4; quality++; pass++; }
                    else if (g == "3") { sum += 3; pass++; }
                    else if (g == "2") { sum += 2; }
                    // Н = 0, не считается
                }

                DataRow perfRow = perf.NewRow();
                perfRow["Предмет"] = subject;
                perfRow["СреднийБалл"] = total > 0 ? Math.Round(sum / total, 2) : 0;
                perfRow["КачествоПроц"] = total > 0 ? Math.Round(100.0 * quality / total, 2) : 0;
                perfRow["УспеваемостьПроц"] = total > 0 ? Math.Round(100.0 * pass / total, 2) : 0;
                perfRow["ВсегоОценок"] = total;
                perf.Rows.Add(perfRow);
            }

            return perf;
        }

        private async void OnExportClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_lastGrades == null || _lastPerf == null)
            {
                await Dialogs.InfoAsync("Экспорт", "Сначала постройте отчёт.");
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить отчёт по сессии",
                DefaultExtension = "xlsx",
                FileTypeChoices = new[] { new FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } } }
            });

            string path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            string group = GroupCombo.SelectedItem as string ?? "";
            var from = DateFromPicker.SelectedDate?.Date ?? DateTime.Today;
            var to = DateToPicker.SelectedDate?.Date ?? DateTime.Today;

            ExportService.ExportSessionReport(_lastGrades, _lastPerf, path, group, from, to);
            await Dialogs.InfoAsync("Экспорт", "Файл сохранён.");
        }
    }
}
