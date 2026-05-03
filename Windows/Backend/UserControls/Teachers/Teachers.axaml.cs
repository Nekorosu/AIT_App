using System;
using System.Collections.Generic;
using System.Data;
using Avalonia.Controls;
using AIT_App.Services;

namespace AIT_App
{
    // Раздел "Преподаватели" (только для администрации).
    // Позволяет добавлять, редактировать и удалять преподавателей.
    // Дополнительно: можно привязать преподавателя к дисциплине.
    public partial class Teachers : UserControl
    {
        private DataBaseCon _db = new DataBaseCon();

        // ФИО редактируемого преподавателя. null = режим добавления.
        // PK таблицы Преподаватели — это ФИО, поэтому используем строку а не int.
        private string _editingFio = null;

        public Teachers()
        {
            InitializeComponent();

            BtnSave.Click += (s, e) => SaveTeacher();
            BtnCancel.Click += (s, e) => ResetForm();
            BtnEdit.Click += (s, e) => StartEdit();
            BtnDelete.Click += (s, e) => DeleteTeacher();

            LoadSubjects();
            LoadTeachers();
        }

        // Загружает все дисциплины для ComboBox
        private void LoadSubjects()
        {
            string sql = "SELECT `Название` FROM `Дисциплины` ORDER BY `Название`";
            var table = _db.ExecuteQuery(sql);

            var subjects = new List<string> { "" }; // первый пустой элемент — "без привязки"
            if (table != null)
                foreach (DataRow row in table.Rows)
                    subjects.Add(row["Название"].ToString());

            SubjectCombo.ItemsSource = subjects;
            SubjectCombo.SelectedIndex = 0;
        }

        // Загружает всех преподавателей в таблицу
        private void LoadTeachers()
        {
            string sql = "SELECT `ФИО`, `Телефон` FROM `Преподаватели` ORDER BY `ФИО`";
            var table = _db.ExecuteQuery(sql);
            TeachersGrid.ItemsSource = DataBaseCon.ToRowList(table);
        }

        // Переходит в режим редактирования выбранного преподавателя
        private async void StartEdit()
        {
            if (TeachersGrid.SelectedItem is not Dictionary<string, object> row)
            {
                await Dialogs.WarnAsync("Редактирование", "Выберите преподавателя в таблице.");
                return;
            }

            _editingFio = row["ФИО"]?.ToString();
            FioInput.Text = _editingFio;
            PhoneInput.Text = row["Телефон"]?.ToString();

            // Ищем дисциплину этого преподавателя
            string sqlSubject = "SELECT `Название` FROM `Дисциплины` WHERE `Преподаватель`=@fio LIMIT 1";
            var subjectResult = _db.ExecuteScalar(sqlSubject,
                new Dictionary<string, object> { { "fio", _editingFio } });
            SubjectCombo.SelectedItem = subjectResult?.ToString() ?? "";

            FormTitle.Text = "Изменить преподавателя";
            BtnCancel.IsVisible = true;
        }

        // Сохраняет преподавателя (новый или изменённый)
        private async void SaveTeacher()
        {
            string fio = FioInput.Text?.Trim();
            string phone = PhoneInput.Text?.Trim();
            string subject = SubjectCombo.SelectedItem as string;

            if (string.IsNullOrEmpty(fio))
            {
                await Dialogs.WarnAsync("Сохранение", "Укажите ФИО.");
                return;
            }

            int result;

            if (_editingFio == null)
            {
                // Добавляем нового преподавателя
                string sql = "INSERT INTO `Преподаватели` (`ФИО`, `Телефон`) VALUES (@fio, @phone)";
                result = _db.ExecuteNonQuery(sql, new Dictionary<string, object>
                {
                    { "fio", fio },
                    { "phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone }
                });
            }
            else
            {
                // Обновляем существующего преподавателя
                string sql = "UPDATE `Преподаватели` SET `ФИО`=@newFio, `Телефон`=@phone WHERE `ФИО`=@oldFio";
                result = _db.ExecuteNonQuery(sql, new Dictionary<string, object>
                {
                    { "newFio", fio },
                    { "phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone },
                    { "oldFio", _editingFio }
                });
            }

            if (result <= 0 && result != -1)
            {
                // -1 = ошибка, но если >= 0 продолжаем
            }

            if (result < 0)
            {
                if (result == -2)
                    await Dialogs.WarnAsync("Сохранение", "Преподаватель с таким ФИО уже существует.");
                else
                    await Dialogs.ErrorAsync("Сохранение", "Не удалось сохранить данные.");
                return;
            }

            // Если выбрана дисциплина — привязываем преподавателя к ней
            if (!string.IsNullOrEmpty(subject))
            {
                string sqlBind = "UPDATE `Дисциплины` SET `Преподаватель`=@fio WHERE `Название`=@subject";
                _db.ExecuteNonQuery(sqlBind, new Dictionary<string, object>
                {
                    { "fio", fio },
                    { "subject", subject }
                });
            }

            ResetForm();
            LoadTeachers();
        }

        // Удаляет преподавателя с подтверждением
        private async void DeleteTeacher()
        {
            if (TeachersGrid.SelectedItem is not Dictionary<string, object> row)
            {
                await Dialogs.WarnAsync("Удаление", "Выберите преподавателя в таблице.");
                return;
            }

            string fio = row["ФИО"]?.ToString();

            bool confirmed = await Dialogs.ConfirmAsync("Удаление",
                $"Удалить преподавателя «{fio}»?\n\n" +
                "Дисциплины этого преподавателя останутся, но без привязки к преподавателю.");
            if (!confirmed) return;

            string sql = "DELETE FROM `Преподаватели` WHERE `ФИО`=@fio";
            int result = _db.ExecuteNonQuery(sql, new Dictionary<string, object> { { "fio", fio } });

            if (result > 0)
            {
                ResetForm();
                LoadTeachers();
            }
            else
                await Dialogs.ErrorAsync("Удаление", "Не удалось удалить преподавателя.");
        }

        // Сбрасывает форму в режим добавления
        private void ResetForm()
        {
            _editingFio = null;
            FioInput.Text = "";
            PhoneInput.Text = "";
            SubjectCombo.SelectedIndex = 0;
            FormTitle.Text = "Добавить преподавателя";
            BtnCancel.IsVisible = false;
            TeachersGrid.SelectedItem = null;
        }
    }
}
