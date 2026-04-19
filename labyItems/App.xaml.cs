using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using labyItems.Controls;
using labyItems.Converters;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Pages;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;

namespace labyItems;

public partial class App : Application
{
    private readonly IDatabaseInitializer _dbInitializer;
    private readonly NavigationPage _mainNavPage;
    private static readonly object FirstChanceSync = new();
    private static readonly HashSet<string> FirstChanceSignatures = new(StringComparer.Ordinal);
    private const int FirstChanceSignatureCap = 64;

    public App(IDatabaseInitializer dbInitializer)
    {
        _dbInitializer = dbInitializer;
        InitializeComponent();
        UserAppTheme = AppTheme.Light;

        _mainNavPage = new NavigationPage(new LoginPage())
        {
            BarTextColor = Colors.White
        };

        MainPage = new NavigationPage(new Pages.StartupDataRefreshPage())
        {
            BarTextColor = Colors.White
        };

        MainPage.Appearing += OnMainPageAppearing;
        RuntimeLog.Write(
            "RUNTIME",
            $"Startup platform={DeviceInfo.Platform} version={DeviceInfo.VersionString} model={DeviceInfo.Model} " +
            $"mono_env='{Environment.GetEnvironmentVariable("MONO_ENV_OPTIONS") ?? "<unset>"}' " +
            $"mono_log_level='{Environment.GetEnvironmentVariable("MONO_LOG_LEVEL") ?? "<unset>"}' " +
            $"mono_log_mask='{Environment.GetEnvironmentVariable("MONO_LOG_MASK") ?? "<unset>"}'");

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            RuntimeLog.Write("UNHANDLED", "Unhandled app-domain exception.", e.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            RuntimeLog.Write("UNOBSERVED", "Unobserved task exception.", e.Exception);
        };
        AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
        {
            TryLogFirstChance(e);
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
                RuntimeLog.Write("DB_INIT", "Database initialization failed.", ex);
            }
            finally
            {
                MainPage.Dispatcher.Dispatch(() => MainPage = _mainNavPage);
            }
        });
    }

    private static void TryLogFirstChance(FirstChanceExceptionEventArgs args)
    {
        var ex = args.Exception;
        if (ex == null)
            return;

        var text = ex.ToString();
        if (!ShouldCaptureFirstChance(text))
            return;

        if (!TryTrackFirstChanceSignature($"{ex.GetType().FullName}|{ex.Message}"))
            return;

        RuntimeLog.Write("FIRST_CHANCE", "Captured first-chance exception matching AOT/persistence filters.", ex);
    }

    private static bool ShouldCaptureFirstChance(string text)
    {
        return text.Contains("sqlite", StringComparison.OrdinalIgnoreCase)
               || text.Contains("database", StringComparison.OrdinalIgnoreCase)
               || text.Contains("attempting to jit compile method", StringComparison.OrdinalIgnoreCase)
               || text.Contains("aot-only mode", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryTrackFirstChanceSignature(string signature)
    {
        lock (FirstChanceSync)
        {
            if (FirstChanceSignatures.Contains(signature))
                return false;

            if (FirstChanceSignatures.Count >= FirstChanceSignatureCap)
                FirstChanceSignatures.Clear();

            FirstChanceSignatures.Add(signature);
            return true;
        }
    }
}
