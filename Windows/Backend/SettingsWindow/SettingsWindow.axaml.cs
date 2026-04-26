using AIT_App.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIT_App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        ConnectionTextBox.Text = ConnectionStringService.Load();

        BtnTest.Click += OnTestClick;
        BtnSave.Click += OnSaveClick;
        BtnClose.Click += (_, _) => Close();
    }

    private async void OnTestClick(object? sender, RoutedEventArgs e)
    {
        var cs = ConnectionTextBox.Text?.Trim() ?? string.Empty;
        BtnTest.IsEnabled = false;
        try
        {
            var (ok, message) = await DataBaseCon.CheckStaticAsync(cs);
            if (ok)
                await Dialogs.InfoAsync("Проверка соединения", "Соединение успешно.");
            else
                await Dialogs.ErrorAsync("Проверка соединения", "Ошибка: " + (message ?? "неизвестно"));
        }
        finally
        {
            BtnTest.IsEnabled = true;
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var cs = ConnectionTextBox.Text?.Trim() ?? string.Empty;
        try
        {
            ConnectionStringService.Save(cs);
        }
        catch (Exception ex)
        {
            await Dialogs.ErrorAsync("Настройки", "Не удалось записать config.json: " + ex.Message);
            return;
        }

        await Dialogs.InfoAsync("Настройки",
            "Строка подключения сохранена.\n" +
            "ВАЖНО: уже открытые разделы продолжают работать со старым подключением. " +
            "Чтобы перечитать строку — выйдите и войдите снова.");
    }
}
