using System.Data;
using AIT_App.Services;
using Avalonia.Controls;
using Avalonia.Data;

namespace AIT_App;

// ТЕСТИРОВЩИК: план сессий — добавление, редактирование (Изменить выбранное),
// удаление с подтверждением, дубликат, переклассификация типов оценок (sp_UpdateGradeTypes).
public partial class PlanSession : UserControl
{
    private readonly DataBaseCon _db = new();

    // ID редактируемой записи. null — режим добавления.
    private int? _editingId;

    public PlanSession()
    {
        InitializeComponent();
        BtnAdd.Click += async (_, _) => await AddAsync();
        BtnEdit.Click += (_, _) => EnterEditMode();
        BtnDelete.Click += async (_, _) => await DeleteAsync();
        BtnSaveEdit.Click += async (_, _) => await SaveEditAsync();
        BtnCancelEdit.Click += (_, _) => ExitEditMode();
        Loaded += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        await LoadSubjectsAsync();
        await ReloadGridAsync();
    }

    private async Task LoadSubjectsAsync()
    {
        const string sql = "SELECT `Название` FROM `Дисциплины` ORDER BY `Название`";
        var table = await _db.ExecuteQueryAsync(sql);
        var list = new List<string>();
        if (table != null)
        {
            foreach (DataRow row in table.Rows)
            {
                var n = row["Название"]?.ToString();
                if (!string.IsNullOrWhiteSpace(n))
                    list.Add(n!);
            }
        }
        else
        {
            await Dialogs.ErrorAsync("План сессий",
                "Не удалось загрузить предметы: " + (_db.LastError ?? "ошибка БД"));
        }

        SubjectCombo.ItemsSource = list;
        if (list.Count > 0)
            SubjectCombo.SelectedIndex = 0;
    }

    private async Task ReloadGridAsync()
    {
        const string sql =
            "SELECT `ID`, `Предмет`, `Дата сессии` FROM `Запланированные_сессии` ORDER BY `Дата сессии`, `Предмет`";
        var table = await _db.ExecuteQueryAsync(sql) ?? new DataTable();
        ConfigureGridColumns(SessionsGrid);
        SessionsGrid.ItemsSource = table.DefaultView;
    }

    private static void ConfigureGridColumns(DataGrid grid)
    {
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Предмет",
            Binding = new Binding
            {
                Path = ".",
                Converter = DataRowColumnValueConverter.Instance,
                ConverterParameter = "Предмет"
            }
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Дата сессии",
            Binding = new Binding
            {
                Path = ".",
                Converter = DataRowColumnValueConverter.Instance,
                ConverterParameter = "Дата сессии"
            }
        });
    }

    // ===== Добавление =====

    private async Task AddAsync()
    {
        if (_editingId is not null)
        {
            // На всякий случай — кнопка «Добавить» в режиме редактирования не должна срабатывать.
            await Dialogs.WarnAsync("План сессий",
                "Сейчас идёт редактирование. Сохраните изменения или нажмите «Отмена».");
            return;
        }

        var subject = SubjectCombo.SelectedItem as string;
        var date = SessionDatePicker.SelectedDate?.Date;
        if (string.IsNullOrWhiteSpace(subject) || date is null)
        {
            await Dialogs.WarnAsync("План сессий", "Выберите предмет и дату.");
            return;
        }

        const string sql =
            """
            INSERT INTO `Запланированные_сессии` (`Дата сессии`, `Предмет`)
            VALUES (@d, @p)
            """;
        var rc = await _db.ExecuteNonQueryAsync(sql, new Dictionary<string, object?>
        {
            ["d"] = date,
            ["p"] = subject
        });

        if (rc == -2)
            await Dialogs.WarnAsync("План сессий", "Такая запись уже существует.");
        else if (rc < 0)
            await Dialogs.ErrorAsync("План сессий",
                "Ошибка при добавлении: " + (_db.LastError ?? "ошибка БД"));
        else
        {
            await CallUpdateGradeTypesAsync();
            await ReloadGridAsync();
        }
    }

    // ===== Удаление =====

    private async Task DeleteAsync()
    {
        if (SessionsGrid.SelectedItem is not DataRowView rv)
        {
            await Dialogs.WarnAsync("План сессий", "Выберите строку в таблице.");
            return;
        }

        var idObj = rv.Row["ID"];
        if (idObj is null or DBNull)
        {
            await Dialogs.ErrorAsync("План сессий", "Не удалось определить ID записи.");
            return;
        }

        var id = Convert.ToInt32(idObj);
        var subject = rv.Row["Предмет"]?.ToString() ?? "";
        var dateStr = rv.Row["Дата сессии"] is DateTime dt ? dt.ToString("dd.MM.yyyy") : "";

        var confirm = await Dialogs.ConfirmAsync(
            "Удаление сессии",
            $"Удалить запись «{subject}» от {dateStr}?\n\n" +
            "Оценки за этот день будут переклассифицированы как «Текущая».");
        if (!confirm)
            return;

        // КОДЕР: в оригинале был лишний ')' в DELETE — здесь только корректный SQL.
        const string sql = "DELETE FROM `Запланированные_сессии` WHERE `ID` = @id";
        var rc = await _db.ExecuteNonQueryAsync(sql, new Dictionary<string, object?> { ["id"] = id });
        if (rc < 0)
        {
            await Dialogs.ErrorAsync("План сессий",
                "Ошибка при удалении: " + (_db.LastError ?? "ошибка БД"));
            return;
        }

        // Если удаляли запись, которую сейчас редактировали — выходим из режима правки.
        if (_editingId == id)
            ExitEditMode();

        await CallUpdateGradeTypesAsync();
        await ReloadGridAsync();
    }

    // ===== Режим редактирования =====

    private void EnterEditMode()
    {
        if (SessionsGrid.SelectedItem is not DataRowView rv)
        {
            _ = Dialogs.WarnAsync("План сессий", "Выберите строку в таблице.");
            return;
        }

        var idObj = rv.Row["ID"];
        if (idObj is null or DBNull)
        {
            _ = Dialogs.ErrorAsync("План сессий", "Не удалось определить ID записи.");
            return;
        }

        _editingId = Convert.ToInt32(idObj);
        var subject = rv.Row["Предмет"]?.ToString();
        SubjectCombo.SelectedItem = subject;
        if (rv.Row["Дата сессии"] is DateTime dt)
            SessionDatePicker.SelectedDate = dt;

        EditModeStatus.Text =
            $"Редактируете запись #{_editingId}: «{subject}» от " +
            (rv.Row["Дата сессии"] is DateTime d ? d.ToString("dd.MM.yyyy") : "?");
        EditModeBar.IsVisible = true;
        BtnAdd.IsEnabled = false;
        BtnEdit.IsEnabled = false;
        BtnDelete.IsEnabled = false;
    }

    private void ExitEditMode()
    {
        _editingId = null;
        EditModeBar.IsVisible = false;
        BtnAdd.IsEnabled = true;
        BtnEdit.IsEnabled = true;
        BtnDelete.IsEnabled = true;
    }

    private async Task SaveEditAsync()
    {
        if (_editingId is null)
            return;

        var subject = SubjectCombo.SelectedItem as string;
        var date = SessionDatePicker.SelectedDate?.Date;
        if (string.IsNullOrWhiteSpace(subject) || date is null)
        {
            await Dialogs.WarnAsync("План сессий", "Заполните предмет и дату.");
            return;
        }

        const string sql =
            "UPDATE `Запланированные_сессии` SET `Предмет`=@p, `Дата сессии`=@d WHERE `ID`=@id";
        var rc = await _db.ExecuteNonQueryAsync(sql, new Dictionary<string, object?>
        {
            ["p"] = subject,
            ["d"] = date,
            ["id"] = _editingId
        });

        if (rc == -2)
        {
            await Dialogs.WarnAsync("План сессий", "Такая запись уже существует.");
            return;
        }

        if (rc < 0)
        {
            await Dialogs.ErrorAsync("План сессий",
                "Ошибка при сохранении: " + (_db.LastError ?? "ошибка БД"));
            return;
        }

        await CallUpdateGradeTypesAsync();
        ExitEditMode();
        await ReloadGridAsync();
    }

    private async Task CallUpdateGradeTypesAsync()
    {
        await _db.ExecuteNonQueryAsync("CALL `sp_UpdateGradeTypes`()");
    }
}
