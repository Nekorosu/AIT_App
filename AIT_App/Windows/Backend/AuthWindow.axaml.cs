using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIT_App;

public partial class AuthWindow : Window
{
    public AuthWindow()
    {
        InitializeComponent();
       
    }

    private void button_Click(object sender, RoutedEventArgs e)
    {

        var session = new SeesionReport();
        session.Show();
        

    }
}