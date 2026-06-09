using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using labyItems.Controls;
using labyItems.Converters;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Pages;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;

namespace labyItems;

public partial class App : Application
{
    private readonly IStartupInitializationService _startupInitializationService;
    private readonly Pages.StartupDataRefreshPage _startupPage;
    private CancellationTokenSource? _startupCts;
    private bool _startupInProgress;
    private bool _startupCompleted;
    private static readonly object FirstChanceSync = new();
    private static readonly HashSet<string> FirstChanceSignatures = new(StringComparer.Ordinal);
    private const int FirstChanceSignatureCap = 64;

    public App(IStartupInitializationService startupInitializationService)
    {
        _startupInitializationService = startupInitializationService;
        InitializeComponent();
        UserAppTheme = AppTheme.Light;

        _startupPage = new Pages.StartupDataRefreshPage();
        _startupPage.RetryRequested += OnRetryRequested;

        MainPage = new NavigationPage(_startupPage)
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

        _ = RunStartupInitializationAsync();
    }

    private void OnRetryRequested(object? sender, EventArgs e)
    {
        _ = RunStartupInitializationAsync(forceRestart: true);
    }

    private async Task RunStartupInitializationAsync(bool forceRestart = false)
    {
        if (_startupCompleted)
            return;

        if (_startupInProgress)
            return;

        _startupInProgress = true;
        _startupCts?.Cancel();
        _startupCts?.Dispose();
        _startupCts = new CancellationTokenSource();

        var cancellationToken = _startupCts.Token;
        var startupStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _startupPage.SetWorking("Starting app", "Preparing startup tasks...");
            });

            var progress = new Progress<StartupProgressInfo>(info =>
            {
                MainThread.BeginInvokeOnMainThread(() => _startupPage.SetWorking(info.Phase, info.Message));
            });

            var result = await _startupInitializationService.InitializeAsync(progress, cancellationToken);
            startupStopwatch.Stop();

            RuntimeLog.Write(
                "STARTUP",
                $"Startup sequence finished in {startupStopwatch.ElapsedMilliseconds} ms. " +
                $"PhaseCount={result.Phases.Count}.");

            var navigationTimer = System.Diagnostics.Stopwatch.StartNew();
            RuntimeLog.Write("STARTUP", "First navigation phase started.");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                MainPage = new NavigationPage(new LoginPage())
                {
                    BarTextColor = Colors.White
                };
            });
            navigationTimer.Stop();
            RuntimeLog.Write("STARTUP", $"First navigation phase completed in {navigationTimer.ElapsedMilliseconds} ms.");

            _startupCompleted = true;
        }
        catch (OperationCanceledException)
        {
            startupStopwatch.Stop();
            RuntimeLog.Write("STARTUP", "Startup sequence was canceled.");

            if (!forceRestart)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _startupPage.SetError(
                        "Startup canceled",
                        "Initialization was canceled. Tap Retry to continue.");
                });
            }
        }
        catch (Exception ex)
        {
            startupStopwatch.Stop();
            RuntimeLog.Write("STARTUP", "Startup sequence failed.", ex);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _startupPage.SetError(
                    "Startup failed",
                    "The app could not finish initialization. You can retry.");
            });
        }
        finally
        {
            _startupInProgress = false;
        }
    }

    private static void TryLogFirstChance(FirstChanceExceptionEventArgs args)
    {
        var ex = args.Exception;
        if (ex == null)
            return;

        if (!ShouldCaptureFirstChance(ex))
            return;

        if (!TryTrackFirstChanceSignature($"{ex.GetType().FullName}|{ex.Message}"))
            return;

        RuntimeLog.Write("FIRST_CHANCE", "Captured first-chance exception matching AOT/persistence filters.", ex);
    }

    private static bool ShouldCaptureFirstChance(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        if (message.Contains("sqlite", StringComparison.OrdinalIgnoreCase)
            || message.Contains("database", StringComparison.OrdinalIgnoreCase)
            || message.Contains("attempting to jit compile method", StringComparison.OrdinalIgnoreCase)
            || message.Contains("aot-only mode", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var typeName = ex.GetType().FullName ?? string.Empty;
        return typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Database", StringComparison.OrdinalIgnoreCase);
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
