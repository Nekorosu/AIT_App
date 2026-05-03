using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIT_App
{
    // Главное окно приложения с боковой навигацией.
    // Принимает логин и роль от AuthWindow:
    //   роль 0 = преподаватель (журнал, отчёты)
    //   роль 1 = администрация (всё + управление студентами, преподавателями, планирование сессий)
    public partial class HomeWindow : Window
    {
        private int _role;   // роль текущего пользователя
        private string _login; // логин текущего пользователя

        // Экземпляры UserControl — создаются один раз и переиспользуются
        private Journal _journal;
        private SessionReport _sessionReport;
        private PlanSession _planSession;
        private Reports _reports;
        private Students _students;
        private Teachers _teachers;

        public HomeWindow(string login, int role)
        {
            InitializeComponent();

            _login = login;
            _role = role;

            // Показываем имя пользователя в нижней части сайдбара
            UserLabel.Text = login;

            // Настраиваем видимость кнопок в зависимости от роли
            if (role == 1)
            {
                // Администрация видит все кнопки
                AdminSeparator.IsVisible = true;
                BtnPlanSession.IsVisible = true;
                BtnStudents.IsVisible = true;
                BtnTeachers.IsVisible = true;
            }

            // Привязываем обработчики к кнопкам навигации
            BtnJournal.Click += (s, e) => ShowJournal();
            BtnSessionReport.Click += (s, e) => ShowSessionReport();
            BtnReports.Click += (s, e) => ShowReports();
            BtnPlanSession.Click += (s, e) => ShowPlanSession();
            BtnStudents.Click += (s, e) => ShowStudents();
            BtnTeachers.Click += (s, e) => ShowTeachers();

            BtnSettings.Click += OnSettingsClick;
            BtnLogout.Click += OnLogoutClick;

            // По умолчанию открываем журнал
            ShowJournal();
        }

        // Методы для переключения разделов.
        // Контрол создаётся при первом обращении (lazy initialization).

        private void ShowJournal()
        {
            if (_journal == null)
                _journal = new Journal();
            MainContent.Content = _journal;
        }

        private void ShowSessionReport()
        {
            if (_sessionReport == null)
                _sessionReport = new SessionReport();
            MainContent.Content = _sessionReport;
        }

        private void ShowReports()
        {
            if (_reports == null)
                _reports = new Reports();
            MainContent.Content = _reports;
        }

        private void ShowPlanSession()
        {
            if (_planSession == null)
                _planSession = new PlanSession();
            MainContent.Content = _planSession;
        }

        private void ShowStudents()
        {
            if (_students == null)
                _students = new Students();
            MainContent.Content = _students;
        }

        private void ShowTeachers()
        {
            if (_teachers == null)
                _teachers = new Teachers();
            MainContent.Content = _teachers;
        }

        // Открывает окно настроек подключения к БД
        private async void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            await settingsWindow.ShowDialog(this);
        }

        // Выход — закрываем главное окно и возвращаемся к авторизации
        private void OnLogoutClick(object sender, RoutedEventArgs e)
        {
            var authWindow = new AuthWindow();
            authWindow.Show();

            if (Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = authWindow;

            this.Close();
        }
    }
}
