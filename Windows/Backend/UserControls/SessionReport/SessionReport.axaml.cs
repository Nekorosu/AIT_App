using System.Data;
using AIT_App.Services;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform.Storage;

namespace AIT_App;

// ТЕСТИРОВЩИК: отчёт по сессии — фильтры группа/диапазон, два грида, экспорт xlsx,
// клик по ячейке с оценкой → правка / удаление с подтверждением.
public partial class SessionReport : UserControl
{
    private readonly DataBaseCon _db = new();
    private DataTable? _lastGrades;
    private DataTable? _lastPerf;

    // Карта (студент, предмет) → дата экзамена.
    // Заполняется при построении pivot, чтобы не дёргать БД на каждый клик.
    private readonly Dictionary<(string Student, string Subject), DateTime> _examDates = new(EqualityComparer<(string, string)>.Default);

    // Состояние выделенной оценки.
    private string? _selectedStudent;
    private string? _selectedSubject;
    private string? _selectedCurrentGrade;
    private DateTime? _selectedExamDate;

    public SessionReport()
    {
        InitializeComponent();

        BtnShow.Click += async (_, _) => await BuildReportAsync();
        BtnExport.Click += async (_, _) => await ExportAsync();

        GroupCombo.SelectionChanged += (_, _) => ClearSelection();

        GradesGrid.CellPointerPressed += OnCellPressed;
        BtnSaveEdit.Click += async (_, _) => await SaveEditAsync();
        BtnDeleteSelected.Click += async (_, _) => await DeleteSelectedAsync();
        BtnCancelSelection.Click += (_, _) => ClearSelection();

        Loaded += async (_, _) =>
        {
            await LoadGroupsAsync();
            await LoadGradeValuesAsync();
        };
    }

    private async Task LoadGroupsAsync()
    {
        const string sql = "SELECT `Название группы` FROM `Группы` ORDER BY `Название группы`";
        var table = await _db.ExecuteQueryAsync(sql);
        var list = new List<string>();
        if (table != null)
        {
            foreach (DataRow row in table.Rows)
            {
                var g = row["Название группы"]?.ToString();
                if (!string.IsNullOrWhiteSpace(g))
                    list.Add(g!);
            }
        }
        else
        {
            await Dialogs.ErrorAsync("Отчёт",
                "Не удалось загрузить группы: " + (_db.LastError ?? "ошибка БД"));
        }

        GroupCombo.ItemsSource = list;
        var today = DateTime.Today;
        DateFromPicker.SelectedDate = today.AddMonths(-1);
        DateToPicker.SelectedDate = today;
    }

    private async Task LoadGradeValuesAsync()
    {
        const string sql = "SELECT `Значение поля` FROM `Оценки` ORDER BY `Значение поля`";
        var table = await _db.ExecuteQueryAsync(sql);
        var grades = new List<string>();
        if (table != null)
        {
            foreach (DataRow row in table.Rows)
            {
                var v = row["Значение поля"]?.ToString();
                if (!string.IsNullOrWhiteSpace(v))
                    grades.Add(v!);
            }
        }

        EditGradeCombo.ItemsSource = grades;
    }

    private async Task BuildReportAsync()
    {
        ClearSelection();

        var group = GroupCombo.SelectedItem as string;
        var from = DateFromPicker.SelectedDate?.Date;
        var to = DateToPicker.SelectedDate?.Date;
        if (string.IsNullOrWhiteSpace(group) || from is null || to is null)
        {
            await Dialogs.WarnAsync("Отчёт", "Выберите группу и диапазон дат.");
            return;
        }

        if (from > to)
        {
            await Dialogs.WarnAsync("Отчёт", "Дата «с» не может быть позже даты «по».");
            return;
        }

        // КОДЕР: добавлено `Дата` в SELECT — нужно для правки/удаления конкретной оценки
        // без дополнительного запроса.
        const string sql =
            """
            SELECT u.`ФИО` AS `ФИО`, d.`Название` AS `Предмет`, o.`Значение поля` AS `Оценка`, a.`Дата` AS `Дата`
            FROM `Ученики` u
            INNER JOIN `Аттестация` a ON a.`Номер студента` = u.`ID`
            INNER JOIN `Дисциплины` d ON a.`Предмет` = d.`Название`
            INNER JOIN `Оценки` o ON a.`Оценка` = o.`Значение поля`
            WHERE u.`Группа` = @group
              AND a.`Тип` = 'Экзаменационная'
              AND a.`Дата` BETWEEN @from AND @to
            ORDER BY u.`ФИО`, d.`Название`
            """;

        var table = await _db.ExecuteQueryAsync(sql, new Dictionary<string, object?>
        {
            ["group"] = group,
            ["from"] = from,
            ["to"] = to
        });

        if (table == null)
        {
            await Dialogs.ErrorAsync("Отчёт",
                "Не удалось выполнить запрос: " + (_db.LastError ?? "ошибка БД"));
            return;
        }

        // Заполняем карту дат для обработки клика по ячейке.
        _examDates.Clear();
        foreach (DataRow row in table.Rows)
        {
            var stu = row["ФИО"]?.ToString();
            var subj = row["Предмет"]?.ToString();
            if (row["Дата"] is DateTime dt && !string.IsNullOrWhiteSpace(stu) && !string.IsNullOrWhiteSpace(subj))
                _examDates[(stu!, subj!)] = dt.Date;
        }

        var pivot = BuildGradesPivot(table);
        var perf = BuildPerformance(table);

        _lastGrades = pivot;
        _lastPerf = perf;

        ConfigureGridColumns(GradesGrid, pivot);
        ConfigureGridColumns(PerfGrid, perf);
        GradesGrid.ItemsSource = pivot.DefaultView;
        PerfGrid.ItemsSource = perf.DefaultView;
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

    private static DataTable BuildGradesPivot(DataTable flat)
    {
        var students = new List<string>();
        var subjects = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DataRow row in flat.Rows)
        {
            var s = row["ФИО"]?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(s) && !students.Contains(s))
                students.Add(s);
            var subj = row["Предмет"]?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(subj))
                subjects.Add(subj);
        }

        var map = new Dictionary<(string Stu, string Subj), string>();
        foreach (DataRow row in flat.Rows)
        {
            var stu = row["ФИО"]?.ToString();
            var subj = row["Предмет"]?.ToString();
            var grade = row["Оценка"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(stu) || string.IsNullOrWhiteSpace(subj))
                continue;
            map[(stu!, subj!)] = grade;
        }

        var pivot = new DataTable();
        pivot.Columns.Add("ФИО", typeof(string));
        foreach (var subj in subjects)
            pivot.Columns.Add(subj, typeof(string));

        foreach (var stu in students.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var r = pivot.NewRow();
            r["ФИО"] = stu;
            foreach (var subj in subjects)
                r[subj] = map.TryGetValue((stu, subj), out var g) ? g : (object)string.Empty;
            pivot.Rows.Add(r);
        }

        return pivot;
    }

    private static DataTable BuildPerformance(DataTable flat)
    {
        var bySubject = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in flat.Rows)
        {
            var subj = row["Предмет"]?.ToString();
            var grade = row["Оценка"]?.ToString();
            if (string.IsNullOrWhiteSpace(subj) || string.IsNullOrWhiteSpace(grade))
                continue;
            if (!bySubject.TryGetValue(subj!, out var list))
            {
                list = [];
                bySubject[subj!] = list;
            }

            list.Add(grade!);
        }

        var perf = new DataTable();
        perf.Columns.Add("Предмет", typeof(string));
        perf.Columns.Add("Средний балл", typeof(double));
        perf.Columns.Add("Качество % (4+5)", typeof(double));
        perf.Columns.Add("Успеваемость % (3+4+5)", typeof(double));
        perf.Columns.Add("Всего оценок", typeof(int));

        foreach (var (subj, grades) in bySubject.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var nums = grades.Select(ParseGrade).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            var total = grades.Count;
            var quality = grades.Count(g => g is "4" or "5");
            var pass = grades.Count(g => g is "3" or "4" or "5");
            var avg = nums.Count > 0 ? nums.Average() : 0d;
            var row = perf.NewRow();
            row["Предмет"] = subj;
            row["Средний балл"] = Math.Round(avg, 2);
            row["Качество % (4+5)"] = total == 0 ? 0 : Math.Round(100.0 * quality / total, 2);
            row["Успеваемость % (3+4+5)"] = total == 0 ? 0 : Math.Round(100.0 * pass / total, 2);
            row["Всего оценок"] = total;
            perf.Rows.Add(row);
        }

        return perf;
    }

    private static int? ParseGrade(string g) => g switch
    {
        "2" => 2,
        "3" => 3,
        "4" => 4,
        "5" => 5,
        _ => null
    };

    // ===== Клик по ячейке + правка/удаление =====

    private void OnCellPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        if (e.Column.Header is not string colHeader)
            return;

        if (colHeader == "ФИО")
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

        EditPanel.IsVisible = true;

        if (string.IsNullOrWhiteSpace(grade))
        {
            _selectedStudent = null;
            _selectedSubject = null;
            _selectedCurrentGrade = null;
            _selectedExamDate = null;
            EditPanelStatus.Text =
                $"У студента «{student}» нет экзаменационной оценки по «{colHeader}» в выбранном диапазоне.";
            BtnSaveEdit.IsEnabled = false;
            BtnDeleteSelected.IsEnabled = false;
            return;
        }

        if (!_examDates.TryGetValue((student!, colHeader), out var examDate))
        {
            // Не должно случиться, но защищаемся.
            ClearSelection();
            return;
        }

        _selectedStudent = student;
        _selectedSubject = colHeader;
        _selectedCurrentGrade = grade;
        _selectedExamDate = examDate;
        EditPanelStatus.Text = $"{student} · {colHeader} · {examDate:dd.MM.yyyy} · оценка: {grade}";
        EditGradeCombo.SelectedItem = grade;
        BtnSaveEdit.IsEnabled = true;
        BtnDeleteSelected.IsEnabled = true;
    }

    private void ClearSelection()
    {
        _selectedStudent = null;
        _selectedSubject = null;
        _selectedCurrentGrade = null;
        _selectedExamDate = null;
        EditPanel.IsVisible = false;
        BtnSaveEdit.IsEnabled = false;
        BtnDeleteSelected.IsEnabled = false;
    }

    private async Task SaveEditAsync()
    {
        if (_selectedStudent is null || _selectedSubject is null || _selectedExamDate is null)
            return;

        var newGrade = EditGradeCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(newGrade))
        {
            await Dialogs.WarnAsync("Отчёт", "Выберите новую оценку.");
            return;
        }

        if (newGrade == _selectedCurrentGrade)
        {
            await Dialogs.InfoAsync("Отчёт", "Оценка не изменилась.");
            return;
        }

        var sid = await ResolveStudentIdAsync(_selectedStudent, GroupCombo.SelectedItem as string);
        if (sid is null)
        {
            await Dialogs.ErrorAsync("Отчёт", "Не удалось найти студента в БД.");
            return;
        }

        const string sql =
            "UPDATE `Аттестация` SET `Оценка`=@g WHERE `Номер студента`=@s AND `Предмет`=@p AND `Дата`=@d";
        var rc = await _db.ExecuteNonQueryAsync(sql, new Dictionary<string, object?>
        {
            ["g"] = newGrade,
            ["s"] = sid,
            ["p"] = _selectedSubject,
            ["d"] = _selectedExamDate
        });

        if (rc < 0)
        {
            await Dialogs.ErrorAsync("Отчёт",
                "Не удалось сохранить: " + (_db.LastError ?? "ошибка БД"));
            return;
        }

        ClearSelection();
        await BuildReportAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedStudent is null || _selectedSubject is null || _selectedExamDate is null || _selectedCurrentGrade is null)
            return;

        var confirm = await Dialogs.ConfirmAsync(
            "Удаление оценки",
            $"Удалить экзаменационную оценку «{_selectedCurrentGrade}» у «{_selectedStudent}» " +
            $"по предмету «{_selectedSubject}» от {_selectedExamDate:dd.MM.yyyy}?\n\nДействие необратимо.");
        if (!confirm)
            return;

        var sid = await ResolveStudentIdAsync(_selectedStudent, GroupCombo.SelectedItem as string);
        if (sid is null)
        {
            await Dialogs.ErrorAsync("Отчёт", "Не удалось найти студента в БД.");
            return;
        }

        const string sql =
            "DELETE FROM `Аттестация` WHERE `Номер студента`=@s AND `Предмет`=@p AND `Дата`=@d";
        var rc = await _db.ExecuteNonQueryAsync(sql, new Dictionary<string, object?>
        {
            ["s"] = sid,
            ["p"] = _selectedSubject,
            ["d"] = _selectedExamDate
        });

        if (rc < 0)
        {
            await Dialogs.ErrorAsync("Отчёт",
                "Не удалось удалить: " + (_db.LastError ?? "ошибка БД"));
            return;
        }

        ClearSelection();
        await BuildReportAsync();
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

    // ===== Экспорт =====

    private async Task ExportAsync()
    {
        if (_lastGrades == null || _lastPerf == null)
        {
            await Dialogs.InfoAsync("Экспорт", "Сначала постройте отчёт.");
            return;
        }

        var group = GroupCombo.SelectedItem as string ?? "";
        var from = DateFromPicker.SelectedDate?.Date ?? DateTime.Today;
        var to = DateToPicker.SelectedDate?.Date ?? DateTime.Today;

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт отчёта по сессии",
            DefaultExtension = "xlsx",
            FileTypeChoices = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await Task.Run(() =>
            ExportService.ExportSessionReport(_lastGrades, _lastPerf, path, group, from, to));
        await Dialogs.InfoAsync("Экспорт", "Файл сохранён.");
    }
}
