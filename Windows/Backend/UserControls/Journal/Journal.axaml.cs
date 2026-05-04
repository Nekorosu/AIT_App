using System;
using System.Collections.Generic;
using System.Data;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AIT_App.Services;

namespace AIT_App
{
    // Раздел "Журнал" — просмотр и редактирование оценок.
    // Таблица плоская: одна строка = одна оценка (ФИО, Предмет, Дата, Оценка, Тип).
    //
    // Клавиатурные сокращения в таблице:
    //   Стрелки ↑↓ — перемещение по строкам
    //   2 3 4 5    — выставить оценку выделенной строке
    //   Н (или H)  — выставить "Н" (не явился)
    public partial class Journal : UserControl
    {
        private DataBaseCon _db = new DataBaseCon();

        // Последняя загруженная таблица — нужна для экспорта
        private DataTable _currentTable = null;

        public Journal()
        {
            InitializeComponent();

            // Заполняем фильтр типов оценок
            TypeCombo.ItemsSource = new[] { "Все", "Текущая", "Экзаменационная" };
            TypeCombo.SelectedIndex = 0;

            // При смене группы — перезагружаем список предметов
            GroupCombo.SelectionChanged += (s, e) => LoadSubjects();

            // Привязываем кнопки
            BtnLoad.Click += (s, e) => LoadJournal();
            BtnToggleAdd.Click += OnToggleAddClick;
            BtnAdd.Click += (s, e) => AddGrade();
            BtnExport.Click += OnExportClick;

            BtnSaveEdit.Click += (s, e) => SaveEdit();
            BtnDelete.Click += (s, e) => DeleteSelected();
            BtnCancelEdit.Click += (s, e) => HideEditPanel();

            // При клике на строку — показываем панель редактирования
            JournalGrid.SelectionChanged += OnGridSelectionChanged;

            // Клавиатурный ввод оценок
            JournalGrid.KeyDown += OnGridKeyDown;

            // Загружаем данные при открытии раздела
            LoadGroups();
            LoadGradeValues();
        }

        // Загружает список групп из БД
        private void LoadGroups()
        {
            string sql = "SELECT `Название группы` FROM `Группы` ORDER BY `Название группы`";
            var table = _db.ExecuteQuery(sql);

            if (table == null)
            {
                _ = Dialogs.ErrorAsync("Журнал", "Не удалось загрузить группы.");
                return;
            }

            var groups = new List<string>();
            foreach (DataRow row in table.Rows)
                groups.Add(row["Название группы"].ToString());

            GroupCombo.ItemsSource = groups;
        }

        // Загружает предметы для выбранной группы
        private void LoadSubjects()
        {
            SubjectCombo.ItemsSource = null;
            SubjectCombo.IsEnabled = false;

            string group = GroupCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(group)) return;

            // Берём только предметы у которых есть оценки в этой группе
            string sql = @"
                SELECT DISTINCT a.`Предмет`
                FROM `Аттестация` a
                INNER JOIN `Ученики` u ON a.`Номер студента` = u.`ID`
                WHERE u.`Группа` = @group
                ORDER BY a.`Предмет`";

            var table = _db.ExecuteQuery(sql, new Dictionary<string, object> { { "group", group } });

            if (table == null || table.Rows.Count == 0)
            {
                // Если оценок нет — показываем все предметы
                sql = "SELECT `Название` FROM `Дисциплины` ORDER BY `Название`";
                table = _db.ExecuteQuery(sql);
            }

            var subjects = new List<string>();
            if (table != null)
                foreach (DataRow row in table.Rows)
                    subjects.Add(row[0].ToString());

            SubjectCombo.ItemsSource = subjects;
            SubjectCombo.IsEnabled = subjects.Count > 0;

            // Обновляем комбобокс предметов в форме добавления
            AddSubjectCombo.ItemsSource = subjects;
            LoadStudents(group);
        }

        // Загружает студентов группы для формы добавления
        private void LoadStudents(string group)
        {
            string sql = "SELECT `ФИО` FROM `Ученики` WHERE `Группа`=@group ORDER BY `ФИО`";
            var table = _db.ExecuteQuery(sql, new Dictionary<string, object> { { "group", group } });

            var students = new List<string>();
            if (table != null)
                foreach (DataRow row in table.Rows)
                    students.Add(row["ФИО"].ToString());

            AddStudentCombo.ItemsSource = students;
        }

        // Загружает допустимые значения оценок из БД
        private void LoadGradeValues()
        {
            string sql = "SELECT `Значение поля` FROM `Оценки` ORDER BY `Значение поля`";
            var table = _db.ExecuteQuery(sql);

            var grades = new List<string>();
            if (table != null)
                foreach (DataRow row in table.Rows)
                    grades.Add(row["Значение поля"].ToString());

            AddGradeCombo.ItemsSource = grades;
            EditGradeCombo.ItemsSource = grades;
        }

        // Загружает оценки в таблицу по выбранным фильтрам
        private void LoadJournal()
        {
            HideEditPanel();

            string group = GroupCombo.SelectedItem as string;
            string subject = SubjectCombo.SelectedItem as string;

            if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(subject))
            {
                _ = Dialogs.WarnAsync("Журнал", "Выберите группу и предмет.");
                return;
            }

            string type = TypeCombo.SelectedItem as string ?? "Все";

            // Строим запрос — если тип "Все", условие по типу не добавляем
            string sql = @"
                SELECT
                    u.`ФИО`,
                    a.`Предмет`,
                    a.`Дата`,
                    a.`Оценка`,
                    a.`Тип`
                FROM `Аттестация` a
                INNER JOIN `Ученики` u ON a.`Номер студента` = u.`ID`
                WHERE u.`Группа` = @group
                  AND a.`Предмет` = @subject"
                + (type != "Все" ? " AND a.`Тип` = @type" : "")
                + " ORDER BY u.`ФИО`, a.`Дата`";

            var parameters = new Dictionary<string, object>
            {
                { "group", group },
                { "subject", subject }
            };
            if (type != "Все")
                parameters.Add("type", type);

            var table = _db.ExecuteQuery(sql, parameters);

            if (table == null)
            {
                _ = Dialogs.ErrorAsync("Журнал", "Не удалось загрузить данные.");
                return;
            }

            _currentTable = table;
            JournalGrid.ItemsSource = DataBaseCon.ToRowList(table);

            StatusLabel.Text = $"Загружено записей: {table.Rows.Count}";
        }

        // Показывает / скрывает панель добавления оценки
        private void OnToggleAddClick(object sender, RoutedEventArgs e)
        {
            AddPanel.IsVisible = !AddPanel.IsVisible;
            if (AddPanel.IsVisible)
                HideEditPanel();
        }

        // Добавляет новую оценку в БД
        private async void AddGrade()
        {
            string studentName = AddStudentCombo.SelectedItem as string;
            string subject = AddSubjectCombo.SelectedItem as string;
            string grade = AddGradeCombo.SelectedItem as string;
            var date = AddDatePicker.SelectedDate?.Date;

            if (string.IsNullOrEmpty(studentName) || string.IsNullOrEmpty(subject)
                || string.IsNullOrEmpty(grade) || date == null)
            {
                await Dialogs.WarnAsync("Добавление", "Заполните все поля.");
                return;
            }

            // Получаем ID студента по ФИО и группе
            string group = GroupCombo.SelectedItem as string;
            int? studentId = GetStudentId(studentName, group);
            if (studentId == null)
            {
                await Dialogs.ErrorAsync("Добавление", "Студент не найден в базе данных.");
                return;
            }

            string sql = @"
                INSERT INTO `Аттестация` (`Оценка`, `Предмет`, `Номер студента`, `Дата`)
                VALUES (@grade, @subject, @studentId, @date)";

            int result = _db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "grade", grade },
                { "subject", subject },
                { "studentId", studentId },
                { "date", date }
            });

            if (result == -2)
                await Dialogs.WarnAsync("Добавление", "Оценка за этот день уже существует.");
            else if (result < 0)
                await Dialogs.ErrorAsync("Добавление", "Ошибка при сохранении в базу данных.");
            else
                LoadJournal(); // перезагружаем таблицу
        }

        // Срабатывает когда пользователь выбирает строку в таблице
        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (JournalGrid.SelectedItem is Dictionary<string, object> row)
            {
                // Показываем панель редактирования с данными выбранной строки
                string fio = row["ФИО"]?.ToString();
                string subject = row["Предмет"]?.ToString();
                string grade = row["Оценка"]?.ToString();
                string dateStr = ParseRowDate(row["Дата"])?.ToString("dd.MM.yyyy") ?? "";

                EditStatusLabel.Text = $"{fio} · {subject} · {dateStr} · оценка: {grade}";
                EditGradeCombo.SelectedItem = grade;
                BtnSaveEdit.IsEnabled = true;
                BtnDelete.IsEnabled = true;
                EditPanel.IsVisible = true;
            }
        }

        // Обработчик клавиш в таблице — 2,3,4,5 или Н выставляют оценку
        private async void OnGridKeyDown(object sender, KeyEventArgs e)
        {
            // Определяем какая оценка соответствует нажатой клавише
            string grade = null;

            if (e.Key == Key.D2 || e.Key == Key.NumPad2) grade = "2";
            else if (e.Key == Key.D3 || e.Key == Key.NumPad3) grade = "3";
            else if (e.Key == Key.D4 || e.Key == Key.NumPad4) grade = "4";
            else if (e.Key == Key.D5 || e.Key == Key.NumPad5) grade = "5";
            else if (e.Key == Key.H) grade = "Н"; // H на латинской = Н на русской раскладке

            if (grade == null) return; // нажата другая клавиша — не обрабатываем

            // Применяем оценку к выделенной строке
            if (JournalGrid.SelectedItem is Dictionary<string, object> row)
            {
                await ApplyGradeToRow(row, grade);
                e.Handled = true; // отмечаем что событие обработано
            }
        }

        // Применяет оценку к конкретной строке в БД
        private async System.Threading.Tasks.Task ApplyGradeToRow(Dictionary<string, object> row, string newGrade)
        {
            string studentName = row["ФИО"]?.ToString();
            string subject = row["Предмет"]?.ToString();
            DateTime? date = ParseRowDate(row["Дата"]);

            if (string.IsNullOrEmpty(studentName) || string.IsNullOrEmpty(subject) || date == null)
                return;

            string group = GroupCombo.SelectedItem as string;
            int? studentId = GetStudentId(studentName, group);
            if (studentId == null) return;

            string sql = @"
                UPDATE `Аттестация`
                SET `Оценка` = @grade
                WHERE `Номер студента` = @studentId
                  AND `Предмет` = @subject
                  AND `Дата` = @date";

            int result = _db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "grade", newGrade },
                { "studentId", studentId },
                { "subject", subject },
                { "date", date }
            });

            if (result > 0)
                LoadJournal(); // перезагружаем чтобы показать новое значение
            else
                await Dialogs.ErrorAsync("Ошибка", "Не удалось обновить оценку.");
        }

        // Сохраняет изменённую оценку из панели редактирования
        private async void SaveEdit()
        {
            if (JournalGrid.SelectedItem is not Dictionary<string, object> row)
                return;

            string newGrade = EditGradeCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(newGrade))
            {
                await Dialogs.WarnAsync("Изменение", "Выберите оценку.");
                return;
            }

            await ApplyGradeToRow(row, newGrade);
            HideEditPanel();
        }

        // Удаляет выделенную запись об оценке
        private async void DeleteSelected()
        {
            if (JournalGrid.SelectedItem is not Dictionary<string, object> row)
                return;

            string fio = row["ФИО"]?.ToString();
            string subject = row["Предмет"]?.ToString();
            string dateStr = ParseRowDate(row["Дата"])?.ToString("dd.MM.yyyy") ?? "";

            bool confirmed = await Dialogs.ConfirmAsync("Удаление",
                $"Удалить оценку студента «{fio}» по предмету «{subject}» от {dateStr}?");
            if (!confirmed) return;

            string group = GroupCombo.SelectedItem as string;
            int? studentId = GetStudentId(fio, group);
            if (studentId == null)
            {
                await Dialogs.ErrorAsync("Удаление", "Студент не найден.");
                return;
            }

            DateTime? date = ParseRowDate(row["Дата"]);

            string sql = @"
                DELETE FROM `Аттестация`
                WHERE `Номер студента` = @studentId
                  AND `Предмет` = @subject
                  AND `Дата` = @date";

            int result = _db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "studentId", studentId },
                { "subject", subject },
                { "date", date }
            });

            if (result > 0)
            {
                HideEditPanel();
                LoadJournal();
            }
            else
                await Dialogs.ErrorAsync("Удаление", "Не удалось удалить запись.");
        }

        // Скрывает панель редактирования и сбрасывает состояние
        private void HideEditPanel()
        {
            EditPanel.IsVisible = false;
            BtnSaveEdit.IsEnabled = false;
            BtnDelete.IsEnabled = false;
            JournalGrid.SelectedItem = null;
        }

        // Вспомогательный метод: парсит дату из ячейки таблицы.
        // MySqlConnector иногда возвращает дату как DateTime, иногда как string —
        // этот метод обрабатывает оба случая.
        private DateTime? ParseRowDate(object value)
        {
            if (value is DateTime dt)
                return dt;
            if (DateTime.TryParse(value?.ToString(), out DateTime parsed))
                return parsed;
            return null;
        }

        // Возвращает ID студента по ФИО и группе
        private int? GetStudentId(string fio, string group)
        {
            string sql = "SELECT `ID` FROM `Ученики` WHERE `ФИО`=@fio AND `Группа`=@group LIMIT 1";
            var result = _db.ExecuteScalar(sql, new Dictionary<string, object>
            {
                { "fio", fio },
                { "group", group }
            });
            return result != null ? (int?)Convert.ToInt32(result) : null;
        }

        // Экспортирует текущую таблицу в Excel
        private async void OnExportClick(object sender, RoutedEventArgs e)
        {
            if (_currentTable == null || _currentTable.Rows.Count == 0)
            {
                await Dialogs.InfoAsync("Экспорт", "Сначала загрузите данные журнала.");
                return;
            }

            // Диалог сохранения файла
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить журнал",
                DefaultExtension = "xlsx",
                FileTypeChoices = new[] { new FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } } }
            });

            string path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            string group = GroupCombo.SelectedItem as string ?? "";
            string subject = SubjectCombo.SelectedItem as string ?? "";

            ExportService.ExportJournal(_currentTable, path, group, subject);
            await Dialogs.InfoAsync("Экспорт", "Файл сохранён.");
        }
    }
}