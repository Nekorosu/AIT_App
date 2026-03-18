using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace AIT_App.ViewModels;

public partial class SessionReportViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Person> _people = new();

    public SessionReportViewModel()
    {
        
        People.Add(new Person("Студентов Игорь Викторович"));
        People.Add(new Person("Студентов Даниила Викторович"));
        People.Add(new Person("Первокурсник Гойда Александрович"));
    }
    
    
}