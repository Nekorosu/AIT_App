using AIT_App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace AIT_App;

public partial class HomeWindow : Window
{
    private readonly string _login;
    private readonly int _role;

    private Journal? _journal;
    private SessionReport? _sessionReport;
    private PlanSession? _planSession;
    private Reports? _reports;

    // ДИЗАЙНЕР: пустой конструктор убирает предупреждение AVLN3001 (превью XAML); в приложении используется перегрузка с логином.
    public HomeWindow()
        : this(string.Empty, 0)
    {
    }

    public HomeWindow(string login, int role)
    {
        InitializeComponent();
        _login = login;
        _role = role;

        WelcomeText.Text = $"Добро пожаловать, {_login}";
        RoleText.Text = role == 0 ? "Роль: преподаватель" : $"Роль: {_role}";

        NavJournal.Click += (_, _) => ShowControl(ref _journal, () => new Journal());
        NavSessionReport.Click += (_, _) => ShowControl(ref _sessionReport, () => new SessionReport());
        NavPlanSession.Click += (_, _) => ShowControl(ref _planSession, () => new PlanSession());
        NavReports.Click += (_, _) => ShowControl(ref _reports, () => new Reports());

        BtnSettings.Click += OnSettingsClick;
        BtnLogout.Click += OnLogoutClick;

        ShowControl(ref _journal, () => new Journal());
    }

    private void ShowControl<T>(ref T? field, Func<T> factory) where T : Control
    {
        field ??= factory();
        ContentArea.Content = field;
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow();
        await dlg.ShowDialog(this);
    }

    private void OnLogoutClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var auth = new AuthWindow();
        auth.Show();
        desktop.MainWindow = auth;
        Close();
    }
}
