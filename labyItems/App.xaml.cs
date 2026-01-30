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
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[APP_XAML] {ex}");
            BuildFallbackResources();
        }
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

    private void BuildFallbackResources()
    {
        Resources ??= new ResourceDictionary();
        AddColors(Resources);
        AddBrushes(Resources);
        Resources["ChevronConverter"] = new BooleanToChevronConverter();
        Resources["InverseBoolConverter"] = new InverseBoolConverter();
        Resources["GreaterThanZeroConverter"] = new GreaterThanZeroConverter();
    }

    private static void AddColors(ResourceDictionary resources)
    {
        resources["Primary"] = Color.FromArgb("#530000");
        resources["Secondary"] = Color.FromArgb("#9A0000");
        resources["Tertiary"] = Color.FromArgb("#FFBD0000");

        resources["White"] = Colors.White;
        resources["Black"] = Colors.Black;
        resources["Magenta"] = Color.FromArgb("#D600AA");
        resources["MidnightBlue"] = Color.FromArgb("#190649");

        resources["Gray100"] = Color.FromArgb("#E1E1E1");
        resources["Gray200"] = Color.FromArgb("#C8C8C8");
        resources["Gray300"] = Color.FromArgb("#ACACAC");
        resources["Gray400"] = Color.FromArgb("#919191");
        resources["Gray500"] = Color.FromArgb("#6E6E6E");
        resources["Gray600"] = Color.FromArgb("#404040");
        resources["Gray900"] = Color.FromArgb("#212121");
        resources["Gray950"] = Color.FromArgb("#141414");
    }

    private static void AddBrushes(ResourceDictionary resources)
    {
        resources["PrimaryBrush"] = new SolidColorBrush((Color)resources["Primary"]);
        resources["SecondaryBrush"] = new SolidColorBrush((Color)resources["Secondary"]);
        resources["TertiaryBrush"] = new SolidColorBrush((Color)resources["Tertiary"]);
        resources["WhiteBrush"] = new SolidColorBrush((Color)resources["White"]);
        resources["BlackBrush"] = new SolidColorBrush((Color)resources["Black"]);
        resources["Gray100Brush"] = new SolidColorBrush((Color)resources["Gray100"]);
        resources["Gray200Brush"] = new SolidColorBrush((Color)resources["Gray200"]);
        resources["Gray300Brush"] = new SolidColorBrush((Color)resources["Gray300"]);
        resources["Gray400Brush"] = new SolidColorBrush((Color)resources["Gray400"]);
        resources["Gray500Brush"] = new SolidColorBrush((Color)resources["Gray500"]);
        resources["Gray600Brush"] = new SolidColorBrush((Color)resources["Gray600"]);
        resources["Gray900Brush"] = new SolidColorBrush((Color)resources["Gray900"]);
        resources["Gray950Brush"] = new SolidColorBrush((Color)resources["Gray950"]);
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
