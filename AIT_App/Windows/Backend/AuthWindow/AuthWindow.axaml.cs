using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIT_App;

public partial class AuthWindow : Window
{
    public AuthWindow()
    {
        InitializeComponent();
       
    }

    private void OpenSession_Click(object sender, RoutedEventArgs e)
    {

        var session = new SessionReport();
        session.Show();
        this.Close();
        

    }
}