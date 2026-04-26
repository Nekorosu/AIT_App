using System.Data;
using AIT_App.Services;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform.Storage;

namespace AIT_App;

// ТЕСТИРОВЩИК: журнал — фильтры, pivot, добавление (дубликат 1062), удаление выделенной оценки,
// изменение оценки кликом по ячейке, триггер типа «Экзаменационная», экспорт xlsx.
public partial class Journal : UserControl
{
    private readonly DataBaseCon _db = new();
    private DataTable? _lastPivot;

    // Карта «строка-заголовок колонки → DateTime». Заполняется при построении pivot.
    private readonly Dictionary<string, DateTime> _columnDateMap = new();

    // Состояние выделенной для редактирования оценки.
    private string? _selectedStudent;
    private DateTime? _selectedDate;
    private string? _selectedCurrentGrade;

    public Journal()
    {
        InitializeComponent();
        TypeFilterCombo.ItemsSource = new[] { "Все", "Текущая", "Экзаменационная" };
        TypeFilterCombo.SelectedIndex = 0;

        GroupCombo.SelectionChanged += async (_, _) => { ClearSelection(); await OnGroupChangedAsync(); };
        SubjectCombo.SelectionChanged += (_, _) => ClearSelection();
        TypeFilterCombo.SelectionChanged += (_, _) => ClearSelection();

        BtnLoad.Click += async (_, _) => await LoadJournalAsync();
        BtnToggleAdd.Click += (_, _) =>
        {
            AddPanel.IsVisible = !AddPanel.IsVisible;
            if (AddPanel.IsVisible) ClearSelection();
        };
        BtnAddGrade.Click += async (_, _) => await AddGradeAsync();
        BtnExport.Click += async (_, _) => await ExportAsync();

        // Клик по ячейке — выделяем оценку для правки/удаления.
        JournalGrid.CellPointerPressed += OnCellPressed;
        BtnSaveEdit.Click += async (_, _) => await SaveEditAsync();
        BtnDeleteSelected.Click += async (_, _) => await DeleteSelectedAsync();
        BtnCancelSelection.Click += (_, _) => ClearSelection();

        Loaded += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        await LoadGroupsAsync();
        await LoadGradeValuesAsync();
    }

    private async Task LoadGroupsAsync()
    {
        const string sql = "SELECT `Название группы` FROM `Группы` ORDER BY `Название группы`";
        var table = await _db.ExecuteQueryAsync(sql);
        if (table == null)
        {
            await Dialogs.ErrorAsync("Журнал", "Не удалось загрузить группы: " + (_db.LastError ?? "ошибка БД"));
            return;
        }
        GroupCombo.ItemsSource = ToStringList(table, "Название группы");
    }

    private static List<string> ToStringList(DataTable? table, string column)
    {
        var list = new List<string>();
        if (table == null)
            return list;
        foreach (DataRow row in table.Rows)
        {
            var v = row[column]?.ToString();
            if (!string.IsNullOrWhiteSpace(v))
                list.Add(v!);
        }

        return list;
    }

    private async Task OnGroupChangedAsync()
    {
        SubjectCombo.ItemsSource = null;
        SubjectCombo.SelectedItem = null;
        SubjectCombo.IsEnabled = false;
        var group = GroupCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(group))
            return;

        const string sql =
            """
            SELECT DISTINCT d.`Название`
            FROM `Дисциплины` d
            ORDER BY d.`Название`
            """;
        var table = await _db.ExecuteQueryAsync(sql);
        var subjects = ToStringList(table, "Название");
        SubjectCombo.ItemsSource = subjects;
        SubjectCombo.IsEnabled = subjects.Count > 0;
        AddStudentCombo.ItemsSource = await LoadStudentsForGroupAsync(group);
    }

    private async Task<List<string>> LoadStudentsForGroupAsync(string group)
    {
        const string sql = "SELECT `ФИО` FROM `Ученики` WHERE `Группа`=@g ORDER BY `ФИО`";
        var table = await _db.ExecuteQueryAsync(sql, new Dictionary<string, object?> { ["g"] = group });
        return ToStringList(table, "ФИО");
    }

    private async Task LoadGradeValuesAsync()
    {
        const string sql = "SELECT `Значение поля` FROM `Оценки` ORDER BY `Значение поля`";
        var table = await _db.ExecuteQueryAsync(sql);
        var grades = ToStringList(table, "Значение поля");
        AddGradeCombo.ItemsSource = grades;
        EditGradeCombo.ItemsSource = grades;
    }

    private async Task LoadJournalAsync()
    {
        ClearSelection();

        var group = GroupCombo.SelectedItem as string;
        var subject = SubjectCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(subject))
        {
            await Dialogs.WarnAsync("Журнал", "Выберите группу и предмет.");
            return;
        }

        var typeFilter = TypeFilterCombo.SelectedItem as string ?? "Все";

        const string sql =
            """
            SELECT u.`ФИО` AS `ФИО`, u.`ID` AS `ID студента`, a.`Дата` AS `Дата`, o.`Значение поля` AS `Оценка`, a.`Тип` AS `Тип`
            FROM `Ученики` u
            LEFT JOIN `Аттестация` a
              ON a.`Номер студента` = u.`ID`
             AND a.`Предмет` = @subject
             AND (@allTypes = 1 OR a.`Тип` = @type)
            LEFT JOIN `Оценки` o ON a.`Оценка` = o.`Значение поля`
            WHERE u.`Группа` = @group
            ORDER BY u.`ФИО`, a.`Дата`
            """;

        var allTypes = typeFilter == "Все" ? 1 : 0;
        var table = await _db.ExecuteQueryAsync(sql, new Dictionary<string, object?>
        {
            ["group"] = group,
            ["subject"] = subject,
            ["allTypes"] = allTypes,
            ["type"] = typeFilter == "Все" ? (object)DBNull.Value : typeFilter
        });

        if (table == null)
        {
            await Dialogs.ErrorAsync("Журнал",
                "Не удалось выполнить запрос: " + (_db.LastError ?? "ошибка БД"));
            return;
        }

        var pivot = BuildPivot(table);
        _lastPivot = pivot;

        // Заполняем карту дат для обработки клика по ячейке.
        _columnDateMap.Clear();
        foreach (DataColumn col in pivot.Columns)
        {
            if (col.ColumnName == "ФИО")
                continue;
            if (DateTime.TryParse(col.ColumnName, out var dt))
                _columnDateMap[col.ColumnName] = dt.Date;
        }

        ConfigureGridColumns(JournalGrid, pivot);
        JournalGrid.ItemsSource = pivot.DefaultView;
        JournalHint.Text = $"Строк: {pivot.Rows.Count}, колонок: {pivot.Columns.Count}. Клик по ячейке — изменить или удалить оценку.";

        AddSubjectCombo.ItemsSource = SubjectCombo.ItemsSource;
        AddSubjectCombo.SelectedItem = subject;
        AddStudentCombo.ItemsSource = await LoadStudentsForGroupAsync(group);
    }

    private static DataTable BuildPivot(DataTable flat)
    {
        var studentNames = new List<string>();
        var studentIds = new Dictionary<string, int>();
        var dates = new SortedSet<DateTime>();

        foreach (DataRow row in flat.Rows)
        {
            var name = row["ФИО"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (!studentIds.ContainsKey(name))
            {
                if (row["ID студента"] == DBNull.Value)
                    continue;
                studentIds[name] = Convert.ToInt32(row["ID студента"]);
                studentNames.Add(name);
            }

            if (row["Дата"] != DBNull.Value && row["Дата"] is DateTime dt)
                dates.Add(dt.Date);
        }

        var pivot = new DataTable();
        pivot.Columns.Add("ФИО", typeof(string));
        foreach (var d in dates)
            pivot.Columns.Add(d.ToString("yyyy-MM-dd"), typeof(string));

        var cellMap = new Dictionary<(string Student, DateTime Date), string>();
        foreach (DataRow row in flat.Rows)
        {
            var name = row["ФИО"]?.ToString();
            if (string.IsNullOrWhiteSpace(name) || row["Дата"] == DBNull.Value)
                continue;
            var date = Convert.ToDateTime(row["Дата"]).Date;
            var grade = row["Оценка"]?.ToString() ?? "";
            var cell = string.IsNullOrWhiteSpace(grade) ? "" : grade;
            cellMap[(name!, date)] = cell;
        }

        foreach (var name in studentNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var r = pivot.NewRow();
            r["ФИО"] = name;
            foreach (DataColumn col in pivot.Columns)
            {
                if (col.ColumnName == "ФИО")
                    continue;
                if (DateTime.TryParse(col.ColumnName, out var day))
                    r[col] = cellMap.TryGetValue((name, day.Date), out var g) ? g : (object)string.Empty;
            }

            pivot.Rows.Add(r);
        }

        return pivot;
    }

    private static void ConfigureGridColumns(DataGrid grid, DataTable table)
    {
        grid.Columns.Clear();
        foreach (DataColumn col in table.Columns)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = col.ColumnName,
                Binding = new Binding
                {
                    Path = ".",
                    Converter = DataRowColumnValueConverter.Instance,
                    ConverterParameter = col.ColumnName
                }
            });
        }
    }

    // ===== Клик по ячейке + правка/удаление =====

    private void OnCellPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        if (e.Column.Header is not string colHeader)
            return;

        // Клик по колонке «ФИО» — не оценка, сбрасываем выделение.
        if (colHeader == "ФИО")
        {
            ClearSelection();
            return;
        }

        if (!_columnDateMap.TryGetValue(colHeader, out var date))
        {
            ClearSelection();
            return;
        }

        if (e.Row.DataContext is not DataRowView drv)
            return;

        var student = drv["ФИО"]?.ToString();
        if (string.IsNullOrWhiteSpace(student))
        {
            ClearSelection();
            return;
        }

        var grade = drv.Row.Table.Columns.Contains(colHeader)
            ? drv[colHeader]?.ToString() ?? ""
            : "";

        // Прячем форму добавления, чтобы не путаться.
        AddPanel.IsVisible = false;
        EditPanel.IsVisible = true;

        if (string.IsNullOrWhiteSpace(grade))
        {
            // Пустая ячейка — сюда нечего изменять/удалять.
            _selectedStudent = null;
            _selectedDate = null;
            _selectedCurrentGrade = null;
            EditPanelStatus.Text =
                $"{student} · {date:dd.MM.yyyy} — оценки нет. Чтобы добавить, нажмите «Добавить оценку».";
            BtnSaveEdit.IsEnabled = false;
            BtnDeleteSelected.IsEnabled = false;
            return;
        }

        _selectedStudent = student;
        _selectedDate = date;
        _selectedCurrentGrade = grade;
        EditPanelStatus.Text = $"{student} · {date:dd.MM.yyyy} · текущая оценка: {grade}";
        EditGradeCombo.SelectedItem = grade;
        BtnSaveEdit.IsEnabled = true;
        BtnDeleteSelected.IsEnabled = true;
    }

    private void ClearSelection()
    {
        _selectedStudent = null;
        _selectedDate = null;
        _selectedCurrentGrade = null;
        EditPanel.IsVisible = false;
        BtnSaveEdit.IsEnabled = false;
        BtnDeleteSelected.IsEnabled = false;
    }

    private async Task SaveEditAsync()
    {
        if (_selectedStudent is null || _selectedDate is null)
            return;

        var newGrade = EditGradeCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(newGrade))
        {
            await Dialogs.WarnAsync("Журнал", "Выберите новую оценку.");
            return;
        }

        if (newGrade == _selectedCurrentGrade)
        {
            await Dialogs.InfoAsync("Журнал", "Оценка не изменилась.");
            return;
        }

        var subject = SubjectCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(subject))
        {
            await Dialogs.WarnAsync("Журнал", "Сначала выберите предмет в фильтре.");
            return;
        }

        var group = GroupCombo.SelectedItem as string;
        var sid = await ResolveStudentIdAsync(_selectedStudent, group);
        if (sid is null)
        {
            await Dialogs.ErrorAsync("Журнал", "Не удалось найти студента в БД.");
            return;
        }

        const string sql =
            "UPDATE `Аттестация` SET `Оценка`=@g WHERE `Номер студента`=@s AND `Предмет`=@p AND `Дата`=@d";
        var rc = await _db.ExecuteNonQueryAsync(sql, new Dictionary<string, object?>
        {
            ["g"] = newGrade,
            ["s"] = sid,
            ["p"] = subject,
            ["d"] = _selectedDate
        });

        if (rc < 0)
        {
            await Dialogs.ErrorAsync("Журнал",
                "Не удалось сохранить: " + (_db.LastError ?? "ошибка БД"));
            return;
        }

        ClearSelection();
        await LoadJournalAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedStudent is null || _selectedDate is null || _selectedCurrentGrade is null)
            return;

        var subject = SubjectCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(subject))
        {
            await Dialogs.WarnAsync("Журнал", "Сначала выберите предмет в фильтре.");
            return;
        }

        var confirm = await Dialogs.ConfirmAsync(
            "Удаление оценки",
            $"Удалить оценку «{_selectedCurrentGrade}» у студента «{_selectedStudent}» от {_selectedDate:dd.MM.yyyy}?\n\nДействие необратимо.");
        if (!confirm)
            return;

        var group = GroupCombo.SelectedItem as string;
        var sid = await ResolveStudentIdAsync(_selectedStudent, group);
        if (sid is null)
        {
            await Dialogs.ErrorAsync("Журнал", "Не удалось найти студента в БД.");
            return;
        }

        const string sql =
            "DELETE FROM `Аттестация` WHERE `Номер студента`=@s AND `Предмет`=@p AND `Дата`=@d";
        var rc = await _db.ExecuteNonQueryAsync(sql, new Dictionary<string, object?>
        {
            ["s"] = sid,
            ["p"] = subject,
            ["d"] = _selectedDate
        });

        if (rc < 0)
        {
            await Dialogs.ErrorAsync("Журнал",
                "Не удалось удалить: " + (_db.LastError ?? "ошибка БД"));
            return;
        }

        ClearSelection();
        await LoadJournalAsync();
    }

    // ===== Добавление =====

    private async Task AddGradeAsync()
    {
        var studentName = AddStudentCombo.SelectedItem as string;
        var subject = AddSubjectCombo.SelectedItem as string;
        var grade = AddGradeCombo.SelectedItem as string;
        var date = AddDatePicker.SelectedDate?.Date;
        if (string.IsNullOrWhiteSpace(studentName) || string.IsNullOrWhiteSpace(subject) ||
            string.IsNullOrWhiteSpace(grade) || date is null)
        {
            await Dialogs.WarnAsync("Журнал", "Заполните студента, предмет, дату и оценку.");
            return;
        }

        var group = GroupCombo.SelectedItem as string;
        var studentId = await ResolveStudentIdAsync(studentName, group);
        if (studentId is null)
        {
            await Dialogs.ErrorAsync("Журнал", "Не удалось определить студента.");
            return;
        }

        const string sql =
            """
            INSERT INTO `Аттестация` (`Оценка`, `Предмет`, `Номер студента`, `Дата`)
            VALUES (@grade, @subject, @sid, @date)
            """;
        var rc = await _db.ExecuteNonQueryAsync(sql, new Dictionary<string, object?>
        {
            ["grade"] = grade,
            ["subject"] = subject,
            ["sid"] = studentId,
            ["date"] = date
        });

        if (rc == -2)
            await Dialogs.WarnAsync("Журнал",
                "Такая оценка уже существует (уникальность предмет+студент+дата).");
        else if (rc < 0)
            await Dialogs.ErrorAsync("Журнал",
                "Ошибка при сохранении: " + (_db.LastError ?? "ошибка БД"));
        else
            await LoadJournalAsync();
    }

    private async Task<int?> ResolveStudentIdAsync(string fio, string? group)
    {
        const string sql = "SELECT `ID` FROM `Ученики` WHERE `ФИО`=@f AND `Группа`=@g LIMIT 1";
        var obj = await _db.ExecuteScalarAsync(sql, new Dictionary<string, object?>
        {
            ["f"] = fio,
            ["g"] = group ?? (object)DBNull.Value
        });
        return obj is null or DBNull ? null : Convert.ToInt32(obj);
    }

    private async Task ExportAsync()
    {
        if (_lastPivot == null || _lastPivot.Rows.Count == 0)
        {
            await Dialogs.InfoAsync("Экспорт", "Нет данных для экспорта — сначала загрузите журнал.");
            return;
        }

        var group = GroupCombo.SelectedItem as string ?? "";
        var subject = SubjectCombo.SelectedItem as string ?? "";
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт журнала",
            DefaultExtension = "xlsx",
            FileTypeChoices =
            [
                new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }
            ]
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await Task.Run(() => ExportService.ExportJournal(_lastPivot, path, group, subject));
        await Dialogs.InfoAsync("Экспорт", "Файл сохранён.");
    }
}
