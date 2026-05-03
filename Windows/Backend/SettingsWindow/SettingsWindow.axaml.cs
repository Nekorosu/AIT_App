using Avalonia.Controls;
using Avalonia.Interactivity;
using AIT_App.Services;

namespace AIT_App
{
    // Окно настроек подключения к базе данных.
    // Позволяет изменить строку подключения и проверить соединение.
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            // Загружаем текущую строку подключения из config.json
            ConnectionTextBox.Text = ConnectionStringService.Load();

            BtnTest.Click += OnTestClick;
            BtnSave.Click += OnSaveClick;
            BtnClose.Click += (s, e) => Close();
        }

        // Проверяет введённую строку подключения
        private async void OnTestClick(object sender, RoutedEventArgs e)
        {
            string connectionString = ConnectionTextBox.Text?.Trim() ?? "";

            // Блокируем кнопку на время проверки
            BtnTest.IsEnabled = false;

            // Проверка происходит в фоне чтобы UI не завис
            var (ok, error) = await System.Threading.Tasks.Task.Run(
                () => DataBaseCon.ConnectionCheck(connectionString));

            BtnTest.IsEnabled = true;

            if (ok)
                await Dialogs.InfoAsync("Проверка", "Соединение успешно установлено.");
            else
                await Dialogs.ErrorAsync("Проверка", "Не удалось подключиться: " + error);
        }

        // Сохраняет строку подключения в config.json
        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string connectionString = ConnectionTextBox.Text?.Trim() ?? "";

            try
            {
                ConnectionStringService.Save(connectionString);
                await Dialogs.InfoAsync("Настройки",
                    "Строка подключения сохранена.\n" +
                    "Чтобы изменения вступили в силу — выйдите и войдите снова.");
            }
            catch (Exception ex)
            {
                await Dialogs.ErrorAsync("Настройки", "Не удалось сохранить: " + ex.Message);
            }
        }
    }
}
