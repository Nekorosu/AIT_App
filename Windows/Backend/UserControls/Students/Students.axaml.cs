using System;
using System.Collections.Generic;
using System.Data;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AIT_App.Services;

namespace AIT_App
{
    // Раздел "Студенты" (только для администрации).
    // Позволяет просматривать, добавлять, редактировать и удалять студентов.
    public partial class Students : UserControl
    {
        private DataBaseCon _db = new DataBaseCon();

        // ID студента которого сейчас редактируем. null = режим добавления.
        private int? _editingId = null;

        public Students()
        {
            InitializeComponent();

            BtnSave.Click += (s, e) => SaveStudent();
            BtnCancel.Click += (s, e) => ResetForm();
            BtnEdit.Click += (s, e) => StartEdit();
            BtnDelete.Click += (s, e) => DeleteStudent();

            // При выборе строки в таблице — подсвечиваем что можно редактировать
            StudentsGrid.SelectionChanged += (s, e) => OnSelectionChanged();

            LoadGroups();
            LoadStudents();
        }

        // Загружает список групп для ComboBox
        private void LoadGroups()
        {
            string sql = "SELECT `Название группы` FROM `Группы` ORDER BY `Название группы`";
            var table = _db.ExecuteQuery(sql);

            var groups = new List<string>();
            if (table != null)
                foreach (DataRow row in table.Rows)
                    groups.Add(row["Название группы"].ToString());

            GroupCombo.ItemsSource = groups;
            if (groups.Count > 0)
                GroupCombo.SelectedIndex = 0;
        }

        // Загружает список всех студентов в таблицу
        private void LoadStudents()
        {
            string sql = @"
                SELECT u.`ID`, u.`ФИО`, u.`Группа`, u.`Телефон`
                FROM `Ученики` u
                ORDER BY u.`Группа`, u.`ФИО`";

            var table = _db.ExecuteQuery(sql);
            StudentsGrid.ItemsSource = DataBaseCon.ToRowList(table);
        }

        // Срабатывает при выборе строки в таблице
        private void OnSelectionChanged()
        {
            // Просто проверяем что что-то выбрано — кнопки всегда активны
        }

        // Переходит в режим редактирования выбранного студента
        private async void StartEdit()
        {
            if (StudentsGrid.SelectedItem is not Dictionary<string, object> row)
            {
                await Dialogs.WarnAsync("Редактирование", "Выберите студента в таблице.");
                return;
            }

            // Заполняем форму данными выбранного студента
            _editingId = Convert.ToInt32(row["ID"]);
            FioInput.Text = row["ФИО"]?.ToString();
            GroupCombo.SelectedItem = row["Группа"]?.ToString();
            PhoneInput.Text = row["Телефон"]?.ToString();

            // Меняем заголовок и показываем кнопку отмены
            FormTitle.Text = "Изменить студента";
            BtnCancel.IsVisible = true;
        }

        // Сохраняет нового студента или изменения существующего
        private async void SaveStudent()
        {
            string fio = FioInput.Text?.Trim();
            string group = GroupCombo.SelectedItem as string;
            string phone = PhoneInput.Text?.Trim();

            // Телефон необязателен, остальные поля обязательны
            if (string.IsNullOrEmpty(fio) || string.IsNullOrEmpty(group))
            {
                await Dialogs.WarnAsync("Сохранение", "Укажите ФИО и группу.");
                return;
            }

            int result;

            if (_editingId == null)
            {
                // Добавляем нового студента
                string sql = @"
                    INSERT INTO `Ученики` (`ФИО`, `Телефон`, `Группа`)
                    VALUES (@fio, @phone, @group)";

                result = _db.ExecuteNonQuery(sql, new Dictionary<string, object>
                {
                    { "fio", fio },
                    { "phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone },
                    { "group", group }
                });
            }
            else
            {
                // Обновляем существующего студента
                string sql = @"
                    UPDATE `Ученики`
                    SET `ФИО` = @fio, `Телефон` = @phone, `Группа` = @group
                    WHERE `ID` = @id";

                result = _db.ExecuteNonQuery(sql, new Dictionary<string, object>
                {
                    { "fio", fio },
                    { "phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone },
                    { "group", group },
                    { "id", _editingId }
                });
            }

            if (result > 0)
            {
                ResetForm();
                LoadStudents();
            }
            else if (result == -2)
                await Dialogs.WarnAsync("Сохранение", "Студент с таким ФИО уже существует в этой группе.");
            else
                await Dialogs.ErrorAsync("Сохранение", "Не удалось сохранить данные.");
        }

        // Удаляет выбранного студента с подтверждением
        private async void DeleteStudent()
        {
            if (StudentsGrid.SelectedItem is not Dictionary<string, object> row)
            {
                await Dialogs.WarnAsync("Удаление", "Выберите студента в таблице.");
                return;
            }

            string fio = row["ФИО"]?.ToString();
            int id = Convert.ToInt32(row["ID"]);

            bool confirmed = await Dialogs.ConfirmAsync("Удаление",
                $"Удалить студента «{fio}»?\n\nВсе его оценки также будут удалены.");
            if (!confirmed) return;

            string sql = "DELETE FROM `Ученики` WHERE `ID` = @id";
            int result = _db.ExecuteNonQuery(sql, new Dictionary<string, object> { { "id", id } });

            if (result > 0)
            {
                ResetForm();
                LoadStudents();
            }
            else
                await Dialogs.ErrorAsync("Удаление", "Не удалось удалить студента.");
        }

        // Сбрасывает форму в режим добавления
        private void ResetForm()
        {
            _editingId = null;
            FioInput.Text = "";
            PhoneInput.Text = "";
            if (GroupCombo.ItemCount > 0)
                GroupCombo.SelectedIndex = 0;

            FormTitle.Text = "Добавить студента";
            BtnCancel.IsVisible = false;
            StudentsGrid.SelectedItem = null;
        }
    }
}
