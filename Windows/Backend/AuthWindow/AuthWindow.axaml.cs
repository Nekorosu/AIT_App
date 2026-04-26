using System.Collections.ObjectModel;
using AIT_App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AIT_App;

// ТЕСТИРОВЩИК: проверьте вход teacher/2222, неверный пароль, недоступную БД,
// анимацию статуса, иконки check/exclamation, Enter в поле пароля = вход.
public partial class AuthWindow : Window
{
    private DataBaseCon _db = new();
    private DispatcherTimer? _dotsTimer;
    private int _dotsPhase;

    public AuthWindow()
    {
        InitializeComponent();
        // КОДЕР: без этого дизайнер Rider может падать на async к БД.
        if (!IsDesignMode)
            Opened += OnOpened;
    }

    private static bool IsDesignMode => Avalonia.Controls.Design.IsDesignMode;

    private async void OnOpened(object? sender, EventArgs e)
    {
        LoginButton.Click += OnLoginClick;
        // ДИЗАЙНЕР: Enter в поле пароля = клик по кнопке «Вход».
        PasswordInput.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && LoginButton.IsEnabled)
                OnLoginClick(this, new RoutedEventArgs());
        };

        StartDotsAnimation();

        // КОДЕР: перечитываем строку каждый раз — SettingsWindow мог её поменять.
        _db = new DataBaseCon(ConnectionStringService.Load());
        var (ok, message) = await _db.ConnectionCheckAsync();
        StopDotsAnimation();

        ConnectionStatusText.Text = ok
            ? "Соединение установлено"
            : "Нет соединения с БД: " + (message ?? "неизвестная ошибка");
        ConnectionIcon.IsVisible = true;
        ConnectionIcon.Source = ok
            ? new Bitmap(AssetLoader.Open(new Uri("avares://AIT_App/Icons/check.png")))
            : new Bitmap(AssetLoader.Open(new Uri("avares://AIT_App/Icons/exclamation.png")));

        LoginButton.IsEnabled = ok;
        if (ok)
            await LoadLoginsAsync();
    }

    private void StartDotsAnimation()
    {
        _dotsPhase = 0;
        _dotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _dotsTimer.Tick += (_, _) =>
        {
            _dotsPhase = (_dotsPhase + 1) % 4;
            var suffix = _dotsPhase switch { 0 => "", 1 => ".", 2 => "..", _ => "..." };
            ConnectionStatusText.Text = "Проверка соединения" + suffix;
        };
        _dotsTimer.Start();
    }

    private void StopDotsAnimation()
    {
        _dotsTimer?.Stop();
        _dotsTimer = null;
    }

    private async Task LoadLoginsAsync()
    {
        const string sql = "SELECT `Логин` FROM `Данные_авторизации` ORDER BY `Логин`";
        var table = await _db.ExecuteQueryAsync(sql);
        var list = new ObservableCollection<string>();
        if (table != null)
        {
            foreach (System.Data.DataRow row in table.Rows)
            {
                var login = row["Логин"]?.ToString();
                if (!string.IsNullOrWhiteSpace(login))
                    list.Add(login!);
            }
        }
        else
        {
            await Dialogs.ErrorAsync("Вход",
                "Не удалось загрузить логины: " + (_db.LastError ?? "неизвестная ошибка"));
        }

        LoginCombo.ItemsSource = list;
        if (list.Count > 0)
            LoginCombo.SelectedIndex = 0;
    }

    private async void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        var login = LoginCombo.SelectedItem as string ?? LoginCombo.Text?.Trim();
        var password = PasswordInput.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            await Dialogs.WarnAsync("Вход", "Укажите логин и пароль.");
            return;
        }

        const string sql =
            "SELECT `Роль` FROM `Данные_авторизации` WHERE `Логин`=@login AND `Пароль`=@password LIMIT 1";
        var roleObj = await _db.ExecuteScalarAsync(sql, new Dictionary<string, object?>
        {
            ["login"] = login,
            ["password"] = password
        });

        if (_db.LastError != null)
        {
            await Dialogs.ErrorAsync("Вход", "Ошибка БД: " + _db.LastError);
            return;
        }

        if (roleObj is null or DBNull)
        {
            await Dialogs.ErrorAsync("Вход", "Неверный логин или пароль.");
            return;
        }

        int role;
        try
        {
            role = Convert.ToInt32(roleObj, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            await Dialogs.ErrorAsync("Вход", "Не удалось прочитать роль из БД: " + ex.Message);
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        HomeWindow home;
        try
        {
            home = new HomeWindow(login!, role);
        }
        catch (Exception ex)
        {
            await Dialogs.ErrorAsync("Вход", "Ошибка при открытии главного окна: " + ex.Message);
            return;
        }

        // КОДЕР: сначала Show(), затем MainWindow, затем Close — иначе classic desktop может завершить процесс,
        // пока новое главное окно ещё не отображено.
        home.Show();
        desktop.MainWindow = home;
        Close();
    }
}
