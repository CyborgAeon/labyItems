using labyItems.Controls;
using labyItems.Converters;
using labyItems.Models;
using labyItems.Pages;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems;

public partial class App : Application
{
    private readonly IDatabaseInitializer _dbInitializer;

    public App(IDatabaseInitializer dbInitializer)
    {
        _dbInitializer = dbInitializer;
        InitializeComponent();
        var navPage = new NavigationPage(new ItemRoutePage())
        {
            BarTextColor = Colors.White
        };
        MainPage = navPage;
        MainPage.Appearing += OnMainPageAppearing;
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[UNHANDLED] {e.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[UNOBSERVED] {e.Exception}");
        };
    }

    private void OnMainPageAppearing(object? sender, EventArgs e)
    {
        if (sender is Page page)
        {
            page.Appearing -= OnMainPageAppearing;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _dbInitializer.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB_INIT] {ex}");
            }
        });
    }
}
