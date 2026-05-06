using System;
using System.Collections.Generic;
using System.Data;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AIT_App.Services;

namespace AIT_App
{
    // Раздел "Планирование сессий" (только для администрации).
    // Позволяет добавлять, редактировать и удалять запланированные экзамены.
    // После каждого изменения вызывается хранимая процедура sp_UpdateGradeTypes —
    // она переклассифицирует все оценки (Текущая / Экзаменационная).
    public partial class PlanSession : UserControl
    {
        private DataBaseCon _db = new DataBaseCon();

        // ID редактируемой записи. null = режим добавления, не null = режим редактирования
        private int? _editingId = null;

        public PlanSession()
        {
            InitializeComponent();

            BtnAdd.Click += (s, e) => AddSession();
            BtnEdit.Click += (s, e) => EnterEditMode();
            BtnDelete.Click += (s, e) => DeleteSession();
            BtnSaveEdit.Click += (s, e) => SaveEdit();
            BtnCancelEdit.Click += (s, e) => ExitEditMode();

            // Загружаем начальные данные
            LoadSubjects();
            LoadSessions();
        }

        // Загружает предметы в ComboBox
        private void LoadSubjects()
        {
            string sql = "SELECT `Название` FROM `Дисциплины` ORDER BY `Название`";
            var table = _db.ExecuteQuery(sql);

            var subjects = new List<string>();
            if (table != null)
                foreach (DataRow row in table.Rows)
                    subjects.Add(row["Название"].ToString());
            else
                _ = Dialogs.ErrorAsync("Ошибка", "Не удалось загрузить предметы.");

            SubjectCombo.ItemsSource = subjects;
            if (subjects.Count > 0)
                SubjectCombo.SelectedIndex = 0;
        }

        // Загружает список сессий в таблицу
        private void LoadSessions()
        {
            string sql = @"
                SELECT `ID`, `Предмет`, `Дата сессии` AS `ДатаСессии`
                FROM `Запланированные_сессии`
                ORDER BY `ДатаСессии`, `Предмет`";

            var table = _db.ExecuteQuery(sql);

            // Если null — показываем пустую таблицу
            SessionsGrid.ItemsSource = DataBaseCon.ToRowList(table);
        }

        // Добавляет новую сессию
        private async void AddSession()
        {
            // В режиме редактирования добавление заблокировано
            if (_editingId != null)
            {
                await Dialogs.WarnAsync("Добавление", "Сначала завершите редактирование.");
                return;
            }

            string subject = SubjectCombo.SelectedItem as string;
            var date = SessionDatePicker.SelectedDate?.Date;

            if (string.IsNullOrEmpty(subject) || date == null)
            {
                await Dialogs.WarnAsync("Добавление", "Выберите предмет и дату.");
                return;
            }

            string sql = @"
                INSERT INTO `Запланированные_сессии` (`Дата сессии`, `Предмет`)
                VALUES (@date, @subject)";

            int result = _db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "date", date },
                { "subject", subject }
            });

            if (result == -2)
                await Dialogs.WarnAsync("Добавление", "Такая сессия уже запланирована.");
            else if (result < 0)
                await Dialogs.ErrorAsync("Добавление", "Ошибка при добавлении.");
            else
            {
                // Переклассифицируем оценки и обновляем таблицу
                CallUpdateGradeTypes();
                LoadSessions();
            }
        }

        // Удаляет выбранную сессию с подтверждением
        private async void DeleteSession()
        {
            if (SessionsGrid.SelectedItem is not Dictionary<string, object> row)
            {
                await Dialogs.WarnAsync("Удаление", "Выберите сессию в таблице.");
                return;
            }

            string subject = row["Предмет"]?.ToString();
            string dateStr = row["ДатаСессии"] is DateTime dt ? dt.ToString("dd.MM.yyyy") : "";
            int id = Convert.ToInt32(row["ID"]);

            // Предупреждаем что оценки будут переклассифицированы
            bool confirmed = await Dialogs.ConfirmAsync("Удаление",
                $"Удалить сессию «{subject}» от {dateStr}?\n\n" +
                "Оценки за этот день станут «Текущими».");
            if (!confirmed) return;

            // Исправлен баг оригинала: лишняя ')' в WHERE была удалена
            string sql = "DELETE FROM `Запланированные_сессии` WHERE `ID` = @id";
            int result = _db.ExecuteNonQuery(sql, new Dictionary<string, object> { { "id", id } });

            if (result < 0)
            {
                await Dialogs.ErrorAsync("Удаление", "Не удалось удалить запись.");
                return;
            }

            // Если удалили ту запись что редактировали — выходим из режима правки
            if (_editingId == id)
                ExitEditMode();

            CallUpdateGradeTypes();
            LoadSessions();
        }

        // Переходит в режим редактирования выбранной строки
        private async void EnterEditMode()
        {
            if (SessionsGrid.SelectedItem is not Dictionary<string, object> row)
            {
                await Dialogs.WarnAsync("Редактирование", "Выберите сессию в таблице.");
                return;
            }

            _editingId = Convert.ToInt32(row["ID"]);
            string subject = row["Предмет"]?.ToString();

            // Заполняем форму данными выбранной строки
            SubjectCombo.SelectedItem = subject;
            if (row["ДатаСессии"] is DateTime dt)
                SessionDatePicker.SelectedDate = dt;

            // Показываем панель режима редактирования
            EditModeLabel.Text = $"Редактируете: «{subject}»";
            EditModeBar.IsVisible = true;

            // Блокируем другие действия пока идёт редактирование
            BtnAdd.IsEnabled = false;
            BtnEdit.IsEnabled = false;
            BtnDelete.IsEnabled = false;
        }

        // Сохраняет изменения и выходит из режима редактирования
        private async void SaveEdit()
        {
            if (_editingId == null) return;

            string subject = SubjectCombo.SelectedItem as string;
            var date = SessionDatePicker.SelectedDate?.Date;

            if (string.IsNullOrEmpty(subject) || date == null)
            {
                await Dialogs.WarnAsync("Редактирование", "Выберите предмет и дату.");
                return;
            }

            string sql = @"
                UPDATE `Запланированные_сессии`
                SET `Предмет` = @subject, `Дата сессии` = @date
                WHERE `ID` = @id";

            int result = _db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "subject", subject },
                { "date", date },
                { "id", _editingId }
            });

            if (result == -2)
            {
                await Dialogs.WarnAsync("Редактирование", "Такая сессия уже существует.");
                return;
            }
            else if (result < 0)
            {
                await Dialogs.ErrorAsync("Редактирование", "Не удалось сохранить изменения.");
                return;
            }

            CallUpdateGradeTypes();
            ExitEditMode();
            LoadSessions();
        }

        // Выходит из режима редактирования без сохранения
        private void ExitEditMode()
        {
            _editingId = null;
            EditModeBar.IsVisible = false;
            BtnAdd.IsEnabled = true;
            BtnEdit.IsEnabled = true;
            BtnDelete.IsEnabled = true;
        }

        // Вызывает хранимую процедуру пересчёта типов оценок
        private void CallUpdateGradeTypes()
        {
            _db.ExecuteNonQuery("CALL `sp_UpdateGradeTypes`()");
        }
    }
}
