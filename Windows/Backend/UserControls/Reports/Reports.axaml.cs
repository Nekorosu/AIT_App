using System;
using System.Collections.Generic;
using System.Data;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AIT_App.Services;

namespace AIT_App
{
    // Раздел "Дополнительные отчёты".
    // Строит пять категорий студентов на основе их оценок:
    //   Отличники, Хорошисты, Троечники, Должники, Кандидаты на красный диплом.
    public partial class Reports : UserControl
    {
        private DataBaseCon _db = new DataBaseCon();

        // Сохраняем таблицы для экспорта в Excel
        private DataTable _tExcellent, _tGood, _tThree, _tDebt, _tRed;

        public Reports()
        {
            InitializeComponent();

            TypeCombo.ItemsSource = new[] { "Все", "Текущая", "Экзаменационная" };
            TypeCombo.SelectedIndex = 0;

            // При смене специальности обновляем список групп
            SpecialityCombo.SelectionChanged += (s, e) => LoadGroups();

            BtnBuild.Click += (s, e) => BuildReports();
            BtnExport.Click += OnExportClick;

            // Загружаем специальности при открытии
            LoadSpecialities();
        }

        private void LoadSpecialities()
        {
            string sql = "SELECT `Код специальности`, `Название` FROM `Специальности` ORDER BY `Название`";
            var table = _db.ExecuteQuery(sql);

            // Первый элемент — "Все специальности"
            var items = new List<string> { "Все специальности" };
            if (table != null)
                foreach (DataRow row in table.Rows)
                    items.Add(row["Название"].ToString());

            SpecialityCombo.ItemsSource = items;
            SpecialityCombo.SelectedIndex = 0;
        }

        // Загружает группы для выбранной специальности
        private void LoadGroups()
        {
            string selectedSpec = SpecialityCombo.SelectedItem as string;
            string sql;
            Dictionary<string, object> parameters = null;

            if (string.IsNullOrEmpty(selectedSpec) || selectedSpec == "Все специальности")
            {
                // Загружаем все группы
                sql = "SELECT `Название группы` FROM `Группы` ORDER BY `Название группы`";
            }
            else
            {
                // Загружаем только группы выбранной специальности
                sql = @"
                    SELECT г.`Название группы`
                    FROM `Группы` г
                    INNER JOIN `Специальности` с ON г.`Специальность` = с.`Код специальности`
                    WHERE с.`Название` = @spec
                    ORDER BY г.`Название группы`";
                parameters = new Dictionary<string, object> { { "spec", selectedSpec } };
            }

            var table = _db.ExecuteQuery(sql, parameters);
            var groups = new List<string> { "Все группы" };
            if (table != null)
                foreach (DataRow row in table.Rows)
                    groups.Add(row["Название группы"].ToString());

            GroupCombo.ItemsSource = groups;
            GroupCombo.SelectedIndex = 0;
        }

        // Строит все пять отчётов
        private void BuildReports()
        {
            string group = GroupCombo.SelectedItem as string ?? "Все группы";
            string type = TypeCombo.SelectedItem as string ?? "Все";

            // Загружаем все оценки с учётом фильтров
            string sql = @"
                SELECT
                    u.`ID` AS StudentID,
                    u.`ФИО`,
                    г.`Название группы` AS Группа,
                    с.`Название` AS Специальность,
                    a.`Предмет`,
                    a.`Оценка`
                FROM `Аттестация` a
                INNER JOIN `Ученики` u ON a.`Номер студента` = u.`ID`
                INNER JOIN `Группы` г ON u.`Группа` = г.`Название группы`
                INNER JOIN `Специальности` с ON г.`Специальность` = с.`Код специальности`"
                + (group != "Все группы" ? " WHERE г.`Название группы` = @group" : "")
                + (type != "Все"
                    ? (group != "Все группы" ? " AND" : " WHERE") + " a.`Тип` = @type"
                    : "");

            var parameters = new Dictionary<string, object>();
            if (group != "Все группы") parameters.Add("group", group);
            if (type != "Все") parameters.Add("type", type);

            var flat = _db.ExecuteQuery(sql, parameters);
            if (flat == null)
            {
                _ = Dialogs.ErrorAsync("Отчёты", "Не удалось загрузить данные.");
                return;
            }

            // Группируем оценки по студентам
            var students = GroupByStudent(flat);

            // Строим таблицы для каждой категории
            _tExcellent = new DataTable();
            _tGood = new DataTable();
            _tThree = new DataTable();
            _tRed = new DataTable();

            // Должники получают дополнительные колонки
            _tDebt = new DataTable();

            AddBaseColumns(_tExcellent);
            AddBaseColumns(_tGood);
            AddBaseColumns(_tThree);
            AddBaseColumns(_tRed);
            _tRed.Columns.Add("СреднийБалл", typeof(double)).Caption = "Средний балл";

            AddBaseColumns(_tDebt);
            _tDebt.Columns.Add("КоличествоДвоек", typeof(int)).Caption = "Кол-во «2»";
            _tDebt.Columns.Add("КоличествоН", typeof(int)).Caption = "Кол-во «Н»";
            _tDebt.Columns.Add("Предметы", typeof(string));

            foreach (var student in students.Values)
            {
                var grades = student.Grades;
                if (grades.Count == 0) continue;

                bool hasTwo = grades.Contains("2");
                bool hasN = grades.Contains("Н");
                bool hasThree = grades.Contains("3");
                bool hasFour = grades.Contains("4");
                bool hasFive = grades.Contains("5");

                // Отличники: все оценки 5
                if (!hasTwo && !hasN && !hasThree && !hasFour)
                    AddBaseRow(_tExcellent, student);

                // Хорошисты: только 4 и 5, есть хотя бы одна 4
                if (!hasTwo && !hasN && !hasThree && hasFour)
                    AddBaseRow(_tGood, student);

                // Троечники: есть 3, нет 2 и Н
                if (hasThree && !hasTwo && !hasN)
                    AddBaseRow(_tThree, student);

                // Должники: есть 2 или Н
                if (hasTwo || hasN)
                {
                    int count2 = 0, countN = 0;
                    var debtSubjects = new List<string>();
                    foreach (var sg in student.SubjectGrades)
                    {
                        if (sg.Value == "2") { count2++; if (!debtSubjects.Contains(sg.Key)) debtSubjects.Add(sg.Key); }
                        if (sg.Value == "Н") { countN++; if (!debtSubjects.Contains(sg.Key)) debtSubjects.Add(sg.Key); }
                    }
                    debtSubjects.Sort();
                    var row = _tDebt.NewRow();
                    row["ФИО"] = student.Fio;
                    row["Группа"] = student.Group;
                    row["Специальность"] = student.Spec;
                    row["КоличествоДвоек"] = count2;
                    row["КоличествоН"] = countN;
                    row["Предметы"] = string.Join(", ", debtSubjects);
                    _tDebt.Rows.Add(row);
                }

                // Красный диплом: нет 3, 2, Н и средний балл >= 4.5
                if (!hasThree && !hasTwo && !hasN)
                {
                    double avg = CalculateAverage(grades);
                    if (avg >= 4.5)
                    {
                        var row = _tRed.NewRow();
                        row["ФИО"] = student.Fio;
                        row["Группа"] = student.Group;
                        row["Специальность"] = student.Spec;
                        row["СреднийБалл"] = Math.Round(avg, 2);
                        _tRed.Rows.Add(row);
                    }
                }
            }

            // Отображаем результаты в таблицах
            GridExcellent.ItemsSource = DataBaseCon.ToRowList(_tExcellent);
            GridGood.ItemsSource = DataBaseCon.ToRowList(_tGood);
            GridThree.ItemsSource = DataBaseCon.ToRowList(_tThree);
            GridDebt.ItemsSource = DataBaseCon.ToRowList(_tDebt);
            GridRed.ItemsSource = DataBaseCon.ToRowList(_tRed);

            // Обновляем счётчики записей
            CntExcellent.Text = $"Найдено: {_tExcellent.Rows.Count}";
            CntGood.Text = $"Найдено: {_tGood.Rows.Count}";
            CntThree.Text = $"Найдено: {_tThree.Rows.Count}";
            CntDebt.Text = $"Найдено: {_tDebt.Rows.Count}";
            CntRed.Text = $"Найдено: {_tRed.Rows.Count}";
        }

        // Группирует строки плоской таблицы по ID студента
        private Dictionary<int, StudentData> GroupByStudent(DataTable flat)
        {
            var result = new Dictionary<int, StudentData>();

            foreach (DataRow row in flat.Rows)
            {
                int id = Convert.ToInt32(row["StudentID"]);
                if (!result.ContainsKey(id))
                {
                    result[id] = new StudentData
                    {
                        Fio = row["ФИО"].ToString(),
                        Group = row["Группа"].ToString(),
                        Spec = row["Специальность"].ToString()
                    };
                }

                result[id].Grades.Add(row["Оценка"].ToString());
                result[id].SubjectGrades.Add(new KeyValuePair<string, string>(
                    row["Предмет"].ToString(), row["Оценка"].ToString()));
            }

            return result;
        }

        // Считает средний балл (Н не учитывается, 2 = 0 для среднего)
        private double CalculateAverage(List<string> grades)
        {
            double sum = 0;
            int count = 0;
            foreach (string g in grades)
            {
                if (g == "5") { sum += 5; count++; }
                else if (g == "4") { sum += 4; count++; }
                else if (g == "3") { sum += 3; count++; }
                else if (g == "2") { sum += 2; count++; }
            }
            return count > 0 ? sum / count : 0;
        }

        // Добавляет базовые колонки ФИО/Группа/Специальность в таблицу
        private void AddBaseColumns(DataTable table)
        {
            table.Columns.Add("ФИО", typeof(string));
            table.Columns.Add("Группа", typeof(string));
            table.Columns.Add("Специальность", typeof(string));
        }

        // Добавляет строку с данными студента в таблицу
        private void AddBaseRow(DataTable table, StudentData student)
        {
            var row = table.NewRow();
            row["ФИО"] = student.Fio;
            row["Группа"] = student.Group;
            row["Специальность"] = student.Spec;
            table.Rows.Add(row);
        }

        private async void OnExportClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_tExcellent == null)
            {
                await Dialogs.InfoAsync("Экспорт", "Сначала постройте отчёт.");
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить отчёты",
                DefaultExtension = "xlsx",
                FileTypeChoices = new[] { new FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } } }
            });

            string path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            var sheets = new Dictionary<string, DataTable>
            {
                { "Отличники", _tExcellent },
                { "Хорошисты", _tGood },
                { "Троечники", _tThree },
                { "Должники", _tDebt },
                { "Красный диплом", _tRed }
            };

            ExportService.ExportReports(sheets, path);
            await Dialogs.InfoAsync("Экспорт", "Файл с 5 листами сохранён.");
        }

        // Вспомогательный класс для хранения данных одного студента
        private class StudentData
        {
            public string Fio { get; set; }
            public string Group { get; set; }
            public string Spec { get; set; }
            public List<string> Grades { get; set; } = new List<string>();
            public List<KeyValuePair<string, string>> SubjectGrades { get; set; } = new List<KeyValuePair<string, string>>();
        }
    }
}
