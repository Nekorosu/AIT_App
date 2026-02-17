using System.Collections.Generic;
using System.Collections.ObjectModel;
using AIT_App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AIT_App;

public partial class SessionReport : Window
{
   
    public SessionReport()
    {
       InitializeComponent();
        #if DEBUG
            this.AttachDevTools();
        #endif
            
            DataContext = new SessionReportViewModel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

public class Person(string fullName)
{
    public string FullName { get; } = fullName;
   
    
    // Равнозначно следующей конструкции:
        /*
           public string FirstName { get; set; }
           public string LastName { get; set; }
           
           public Person(string firstName , string lastName)
           {
               FirstName = firstName;
               LastName = lastName;
           }
           
         */
}