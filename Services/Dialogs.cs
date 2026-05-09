using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;

namespace AIT_App.Services
{
    public static class Dialogs
    {
        public static async Task InfoAsync(string title, string text)
            => await ShowAsync(title, text, "ℹ", "#2196F3");

        public static async Task WarnAsync(string title, string text)
            => await ShowAsync(title, text, "⚠", "#FF9800");

        public static async Task ErrorAsync(string title, string text)
            => await ShowAsync(title, text, "✕", "#F44336");

        public static async Task<bool> ConfirmAsync(string title, string text)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 360,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                CanResize = false
            };

            bool result = false;

            var yesBtn = new Button { Content = "Да", Width = 80 };
            var noBtn = new Button { Content = "Нет", Width = 80 };

            yesBtn.Click += (s, e) => { result = true; dialog.Close(); };
            noBtn.Click += (s, e) => { result = false; dialog.Close(); };

            dialog.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { yesBtn, noBtn }
                    }
                }
            };

            await dialog.ShowDialog(GetMainWindow());
            return result;
        }

        private static async Task ShowAsync(string title, string text, string icon, string colorHex)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 360,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                CanResize = false
            };

            var btn = new Button { Content = "ОК", Width = 80,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            btn.Click += (s, e) => dialog.Close();

            dialog.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = icon,
                                FontSize = 18,
                                Foreground = SolidColorBrush.Parse(colorHex),
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = title,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = SolidColorBrush.Parse(colorHex),
                                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                            }
                        }
                    },
                    new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    btn
                }
            };

            await dialog.ShowDialog(GetMainWindow());
        }

        private static Window GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }
    }
}
