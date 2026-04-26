using System.Data;
using AIT_App.Services;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform.Storage;

namespace AIT_App;

// ТЕСТИРОВЩИК: отчёты — фильтры специальность/группа/тип, пять вкладок, счётчики, один xlsx с 5 листами.
public partial class Reports : UserControl
{
    private readonly DataBaseCon _db = new();

    private DataTable? _dtExcellent;
    private DataTable? _dtGood;
    private DataTable? _dtThree;
    private DataTable? _dtDebt;
    private DataTable? _dtRed;

    private sealed record SpecItem(string Code, string Name)
    {
        public override string ToString() => Name;
    }

    public Reports()
    {
        InitializeComponent();
        TypeCombo.ItemsSource = new[] { "Все", "Текущая", "Экзаменационная" };
        TypeCombo.SelectedIndex = 0;

        Loaded += async (_, _) => await InitAsync();
        SpecialityCombo.SelectionChanged += async (_, _) => await OnSpecialityChangedAsync();
        BtnBuild.Click += async (_, _) => await BuildAsync();
        BtnExport.Click += async (_, _) => await ExportAsync();
    }

    private async Task InitAsync()
    {
        const string sql =
            """
            SELECT `Код специальности`, `Название`
            FROM `Специальности`
            ORDER BY `Название`
            """;
        var table = await _db.ExecuteQueryAsync(sql);
        var items = new List<SpecItem> { new("", "Все специальности") };
        if (table != null)
        {
            foreach (DataRow row in table.Rows)
            {
                var code = row["Код специальности"]?.ToString() ?? "";
                var name = row["Название"]?.ToString() ?? "";
                items.Add(new SpecItem(code, string.IsNullOrWhiteSpace(name) ? code : name));
            }
        }
        else
        {
            await Dialogs.ErrorAsync("Отчёты",
                "Не удалось загрузить специальности: " + (_db.LastError ?? "ошибка БД"));
        }

        SpecialityCombo.ItemsSource = items;
        SpecialityCombo.SelectedIndex = 0;
        await OnSpecialityChangedAsync();
    }

    private async Task OnSpecialityChangedAsync()
    {
        GroupCombo.ItemsSource = null;
        GroupCombo.SelectedItem = null;
        GroupCombo.IsEnabled = false;

        if (SpecialityCombo.SelectedItem is not SpecItem spec || string.IsNullOrWhiteSpace(spec.Code))
        {
            GroupCombo.IsEnabled = true;
            GroupCombo.ItemsSource = await LoadAllGroupsAsync();
            GroupCombo.SelectedIndex = 0;
            return;
        }

        const string sql =
            """
            SELECT `Название группы`
            FROM `Группы`
            WHERE `Специальность` = @c
            ORDER BY `Название группы`
            """;
        var table = await _db.ExecuteQueryAsync(sql, new Dictionary<string, object?> { ["c"] = spec.Code });
        var groups = new List<string> { "Все группы" };
        if (table != null)
        {
            foreach (DataRow row in table.Rows)
            {
                var g = row["Название группы"]?.ToString();
                if (!string.IsNullOrWhiteSpace(g))
                    groups.Add(g!);
            }
        }

        GroupCombo.ItemsSource = groups;
        GroupCombo.SelectedIndex = 0;
        GroupCombo.IsEnabled = true;
    }

    private async Task<List<string>> LoadAllGroupsAsync()
    {
        const string sql = "SELECT `Название группы` FROM `Группы` ORDER BY `Название группы`";
        var table = await _db.ExecuteQueryAsync(sql);
        var groups = new List<string> { "Все группы" };
        if (table != null)
        {
            foreach (DataRow row in table.Rows)
            {
                var g = row["Название группы"]?.ToString();
                if (!string.IsNullOrWhiteSpace(g))
                    groups.Add(g!);
            }
        }

        return groups;
    }

    private async Task BuildAsync()
    {
        var spec = SpecialityCombo.SelectedItem as SpecItem;
        var groupItem = GroupCombo.SelectedItem as string;
        var typeItem = TypeCombo.SelectedItem as string ?? "Все";

        var allSpec = spec is null || string.IsNullOrWhiteSpace(spec.Code) ? 1 : 0;
        var allGroup = string.IsNullOrWhiteSpace(groupItem) || groupItem == "Все группы" ? 1 : 0;
        var allType = typeItem == "Все" ? 1 : 0;

        const string sql =
            """
            SELECT
              u.`ID` AS `ID студента`,
              u.`ФИО` AS `ФИО`,
              g.`Название группы` AS `Группа`,
              s.`Название` AS `Специальность`,
              s.`Код специальности` AS `Код специальности`,
              d.`Название` AS `Предмет`,
              o.`Значение поля` AS `Оценка`,
              a.`Тип` AS `Тип`
            FROM `Аттестация` a
            INNER JOIN `Ученики` u ON a.`Номер студента` = u.`ID`
            INNER JOIN `Группы` g ON u.`Группа` = g.`Название группы`
            INNER JOIN `Специальности` s ON g.`Код специальности` = s.`Код специальности`
            INNER JOIN `Дисциплины` d ON a.`Предмет` = d.`Название`
            INNER JOIN `Оценки` o ON a.`Оценка` = o.`Значение поля`
            WHERE (@allSpec = 1 OR s.`Код специальности` = @specCode)
              AND (@allGroup = 1 OR g.`Название группы` = @groupName)
              AND (@allType = 1 OR a.`Тип` = @gradeType)
            """;

        var table = await _db.ExecuteQueryAsync(sql, new Dictionary<string, object?>
        {
            ["allSpec"] = allSpec,
            ["specCode"] = spec?.Code ?? (object)DBNull.Value,
            ["allGroup"] = allGroup,
            ["groupName"] = allGroup == 1 ? (object)DBNull.Value : groupItem!,
            ["allType"] = allType,
            ["gradeType"] = allType == 1 ? (object)DBNull.Value : typeItem
        });

        if (table == null)
        {
            await Dialogs.ErrorAsync("Отчёты",
                "Не удалось загрузить данные: " + (_db.LastError ?? "ошибка БД"));
            return;
        }

        BuildTables(table,
            out var tEx,
            out var tGood,
            out var tThree,
            out var tDebt,
            out var tRed);

        _dtExcellent = tEx;
        _dtGood = tGood;
        _dtThree = tThree;
        _dtDebt = tDebt;
        _dtRed = tRed;

        Bind(GridExcellent, CntExcellent, tEx);
        Bind(GridGood, CntGood, tGood);
        Bind(GridThree, CntThree, tThree);
        Bind(GridDebt, CntDebt, tDebt);
        Bind(GridRed, CntRed, tRed);
    }

    private static void Bind(DataGrid grid, TextBlock label, DataTable t)
    {
        ConfigureGridColumns(grid, t);
        grid.ItemsSource = t.DefaultView;
        label.Text = $"Записей: {t.Rows.Count}";
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

    private static void BuildTables(
        DataTable flat,
        out DataTable excellent,
        out DataTable good,
        out DataTable three,
        out DataTable debt,
        out DataTable red)
    {
        var byStudent = new Dictionary<int, StudentGrades>();

        foreach (DataRow row in flat.Rows)
        {
            var id = Convert.ToInt32(row["ID студента"]);
            if (!byStudent.TryGetValue(id, out var sg))
            {
                sg = new StudentGrades(
                    id,
                    row["ФИО"]?.ToString() ?? "",
                    row["Группа"]?.ToString() ?? "",
                    row["Специальность"]?.ToString() ?? "");
                byStudent[id] = sg;
            }

            sg.Grades.Add(row["Оценка"]?.ToString() ?? "");
            sg.SubjectGrades.Add((row["Предмет"]?.ToString() ?? "", row["Оценка"]?.ToString() ?? ""));
        }

        excellent = CreateTable("ФИО", "Группа", "Специальность");
        good = CreateTable("ФИО", "Группа", "Специальность");
        three = CreateTable("ФИО", "Группа", "Специальность");
        debt = new DataTable();
        debt.Columns.Add("ФИО", typeof(string));
        debt.Columns.Add("Группа", typeof(string));
        debt.Columns.Add("Специальность", typeof(string));
        debt.Columns.Add("Количество «2»", typeof(int));
        debt.Columns.Add("Количество «Н»", typeof(int));
        debt.Columns.Add("Предметы (2 и Н)", typeof(string));
        red = new DataTable();
        red.Columns.Add("ФИО", typeof(string));
        red.Columns.Add("Группа", typeof(string));
        red.Columns.Add("Специальность", typeof(string));
        red.Columns.Add("Средний балл", typeof(double));

        foreach (var sg in byStudent.Values)
        {
            var grades = sg.Grades;
            if (grades.Count == 0)
                continue;

            if (grades.All(g => g == "5"))
                AddRow(excellent, sg);

            if (grades.All(g => g is "4" or "5") && grades.Any(g => g == "4"))
                AddRow(good, sg);

            if (grades.Any(g => g == "3") && grades.All(g => g is not "2" and not "Н"))
                AddRow(three, sg);

            if (grades.Any(g => g is "2" or "Н"))
            {
                var c2 = grades.Count(g => g == "2");
                var cn = grades.Count(g => g == "Н");
                var badSubjects = sg.SubjectGrades
                    .Where(p => p.Grade is "2" or "Н")
                    .Select(p => p.Subject)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
                var r = debt.NewRow();
                r["ФИО"] = sg.Fio;
                r["Группа"] = sg.Group;
                r["Специальность"] = sg.Spec;
                r["Количество «2»"] = c2;
                r["Количество «Н»"] = cn;
                r["Предметы (2 и Н)"] = string.Join(", ", badSubjects);
                debt.Rows.Add(r);
            }

            if (!grades.Any(g => g is "3" or "2" or "Н"))
            {
                var nums = grades.Select(Parse).Where(v => v.HasValue).Select(v => v!.Value).ToList();
                if (nums.Count > 0)
                {
                    var avg = nums.Average();
                    if (avg >= 4.5d)
                    {
                        var rr = red.NewRow();
                        rr["ФИО"] = sg.Fio;
                        rr["Группа"] = sg.Group;
                        rr["Специальность"] = sg.Spec;
                        rr["Средний балл"] = Math.Round(avg, 2);
                        red.Rows.Add(rr);
                    }
                }
            }
        }
    }

    private sealed class StudentGrades
    {
        public StudentGrades(int id, string fio, string group, string spec)
        {
            Id = id;
            Fio = fio;
            Group = group;
            Spec = spec;
        }

        public int Id { get; }
        public string Fio { get; }
        public string Group { get; }
        public string Spec { get; }
        public List<string> Grades { get; } = [];
        public List<(string Subject, string Grade)> SubjectGrades { get; } = [];
    }

    private static DataTable CreateTable(params string[] cols)
    {
        var t = new DataTable();
        foreach (var c in cols)
            t.Columns.Add(c);
        return t;
    }

    private static void AddRow(DataTable t, StudentGrades sg)
    {
        var r = t.NewRow();
        r["ФИО"] = sg.Fio;
        r["Группа"] = sg.Group;
        r["Специальность"] = sg.Spec;
        t.Rows.Add(r);
    }

    private static int? Parse(string g) => g switch
    {
        "2" => 2,
        "3" => 3,
        "4" => 4,
        "5" => 5,
        _ => null
    };

    private async Task ExportAsync()
    {
        if (_dtExcellent == null || _dtGood == null || _dtThree == null || _dtDebt == null || _dtRed == null)
        {
            await Dialogs.InfoAsync("Экспорт", "Сначала постройте отчёт.");
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт отчётов",
            DefaultExtension = "xlsx",
            FileTypeChoices = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        var sheets = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase)
        {
            ["Отличники"] = _dtExcellent,
            ["Хорошисты"] = _dtGood,
            ["Троечники"] = _dtThree,
            ["Должники"] = _dtDebt,
            ["Красный диплом"] = _dtRed
        };

        await Task.Run(() => ExportService.ExportReports(sheets, path));
        await Dialogs.InfoAsync("Экспорт", "Файл с 5 листами сохранён.");
    }
}
