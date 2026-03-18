using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    private void Test_BD_Click(object sender, RoutedEventArgs e)
    {
        using (var context = new ElectronicJournal())
        {
            var list = context.Students.ToList();
            foreach (var student in list)
            {
                Console.WriteLine($"{student.FullName} получил идентификатор {student.ID}");
            }
        }
    }
}

//-------------------------------------------------------------------//

public class Student
{
    public int ID { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string Group { get; set; }
}

public class ElectronicJournal : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var ConnectionString = "server=192.168.32.5;database=electronic_journal_test;uid=eugh;password=ust$*0Moq23;";
        optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
    }
    public DbSet<Student> Students { get; set; }
    
    
}

