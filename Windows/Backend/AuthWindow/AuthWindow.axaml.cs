using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AIT_App.Services;

namespace AIT_App
{
    // Окно авторизации — первое что видит пользователь.
    // При открытии проверяет соединение с БД и загружает список логинов.
    public partial class AuthWindow : Window
    {
        private DataBaseCon _db = new DataBaseCon();
        private DispatcherTimer _dotsTimer; // таймер для анимации точек
        private int _dotsCount = 0;         // счётчик точек (0..3)

        public AuthWindow()
        {
            InitializeComponent();

            // Не запускаем обращения к БД в режиме дизайнера Rider
            if (!Avalonia.Controls.Design.IsDesignMode)
            {
                Opened += OnWindowOpened;
            }
        }

        // Срабатывает когда окно полностью открылось
        private async void OnWindowOpened(object sender, EventArgs e)
        {
            // Привязываем Enter в поле пароля к кнопке входа
            PasswordInput.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Enter)
                    OnLoginClick(this, new RoutedEventArgs());
            };

            LoginButton.Click += OnLoginClick;

            // Запускаем анимацию точек пока идёт проверка
            StartDotsAnimation();

            // Task.Run нужен чтобы UI не завис во время проверки соединения
            var (ok, error) = await System.Threading.Tasks.Task.Run(() => _db.ConnectionCheck());

            StopDotsAnimation();

            if (ok)
            {
                // Соединение успешно — показываем иконку галочки и загружаем логины
                ConnectionStatusText.Text = "Соединение установлено";
                ConnectionIcon.IsVisible = true;
                ConnectionIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://AIT_App/Icons/check.png")));
                LoginButton.IsEnabled = true;
                LoadLogins();
            }
            else
            {
                // Соединение не удалось — показываем ошибку
                ConnectionStatusText.Text = "Нет соединения: " + error;
                ConnectionIcon.IsVisible = true;
                ConnectionIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://AIT_App/Icons/check.png")));
                LoginButton.IsEnabled = false;
            }
        }

        // Запускает анимацию "Подключение..." -> "Подключение." -> ".." -> "..."
        private void StartDotsAnimation()
        {
            _dotsCount = 0;
            _dotsTimer = new DispatcherTimer();
            _dotsTimer.Interval = TimeSpan.FromMilliseconds(400);
            _dotsTimer.Tick += (s, e) =>
            {
                _dotsCount = (_dotsCount + 1) % 4;
                string dots = new string('.', _dotsCount);
                ConnectionStatusText.Text = "Проверка соединения" + dots;
            };
            _dotsTimer.Start();
        }

        private void StopDotsAnimation()
        {
            _dotsTimer?.Stop();
            _dotsTimer = null;
        }

        // Загружает список логинов из таблицы Данные_авторизации в ComboBox
        private void LoadLogins()
        {
            string sql = "SELECT `Логин` FROM `Данные_авторизации` ORDER BY `Логин`";
            var table = _db.ExecuteQuery(sql);

            if (table == null)
            {
                _ = Dialogs.ErrorAsync("Ошибка", "Не удалось загрузить список пользователей.");
                return;
            }

            var logins = new System.Collections.Generic.List<string>();
            foreach (System.Data.DataRow row in table.Rows)
                logins.Add(row["Логин"].ToString());

            LoginCombo.ItemsSource = logins;
            if (logins.Count > 0)
                LoginCombo.SelectedIndex = 0;
        }

        // Обработчик нажатия кнопки "Вход"
        private async void OnLoginClick(object sender, RoutedEventArgs e)
        {
            string login = LoginCombo.SelectedItem as string ?? LoginCombo.Text?.Trim();
            string password = PasswordInput.Text ?? "";

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                await Dialogs.WarnAsync("Вход", "Введите логин и пароль.");
                return;
            }

            // Проверяем логин и пароль в БД, получаем роль пользователя
            string sql = "SELECT `Роль` FROM `Данные_авторизации` WHERE `Логин`=@login AND `Пароль`=@password LIMIT 1";
            var result = _db.ExecuteScalar(sql, new Dictionary<string, object>
            {
                { "login", login },
                { "password", password }
            });

            if (result == null)
            {
                // Пользователь с таким логином/паролем не найден
                await Dialogs.ErrorAsync("Вход", "Неверный логин или пароль.");
                return;
            }

            // Преобразуем роль из БД в число
            int role = Convert.ToInt32(result);

            // Открываем главное окно, передаём логин и роль
            var homeWindow = new HomeWindow(login, role);
            homeWindow.Show();

            // Меняем главное окно приложения и закрываем авторизацию
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = homeWindow;

            this.Close();
        }
    }
}
