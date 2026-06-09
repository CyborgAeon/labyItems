using System.Diagnostics;
using labyItems.Helpers;

namespace labyItems.Services;

public sealed record StartupPhaseTiming(string PhaseName, TimeSpan Duration);

public sealed record StartupInitializationResult(TimeSpan TotalDuration, IReadOnlyList<StartupPhaseTiming> Phases);

public sealed record StartupProgressInfo(string Phase, string Message, double? Percent = null);

public interface IStartupInitializationService
{
    Task<StartupInitializationResult> InitializeAsync(IProgress<StartupProgressInfo>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class StartupInitializationService : IStartupInitializationService
{
    private readonly ICacheMaintenanceService _cacheMaintenanceService;
    private readonly IDatabaseInitializer _databaseInitializer;

    public StartupInitializationService(
        ICacheMaintenanceService cacheMaintenanceService,
        IDatabaseInitializer databaseInitializer)
    {
        _cacheMaintenanceService = cacheMaintenanceService;
        _databaseInitializer = databaseInitializer;
    }

    public async Task<StartupInitializationResult> InitializeAsync(IProgress<StartupProgressInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        var phases = new List<StartupPhaseTiming>();
        var total = Stopwatch.StartNew();

        RuntimeLog.Write(
            "STARTUP",
            $"Startup initialization started. threadId={Environment.CurrentManagedThreadId}.");

        phases.Add(await RunPhaseAsync(
            phase: "Cache cleanup",
            message: "Cleaning cached temporary files...",
            percent: 0.35,
            action: ct => _cacheMaintenanceService.RunStartupCleanupAsync(ct),
            progress: progress,
            cancellationToken: cancellationToken));

        phases.Add(await RunPhaseAsync(
            phase: "Database initialization",
            message: "Preparing database, migrations, and startup data...",
            percent: 0.85,
            action: ct => Task.Run(() => _databaseInitializer.InitializeAsync(ct), ct),
            progress: progress,
            cancellationToken: cancellationToken));

        total.Stop();
        progress?.Report(new StartupProgressInfo("Complete", "Startup initialization complete.", 1.0));

        RuntimeLog.Write(
            "STARTUP",
            $"Startup initialization completed in {total.ElapsedMilliseconds} ms.");

        return new StartupInitializationResult(total.Elapsed, phases);
    }

    private static async Task<StartupPhaseTiming> RunPhaseAsync(
        string phase,
        string message,
        double percent,
        Func<CancellationToken, Task> action,
        IProgress<StartupProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new StartupProgressInfo(phase, message, percent));

        var stopwatch = Stopwatch.StartNew();
        RuntimeLog.Write("STARTUP", $"Phase '{phase}' started.");

        try
        {
            await action(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            RuntimeLog.Write("STARTUP", $"Phase '{phase}' completed in {stopwatch.ElapsedMilliseconds} ms.");
            return new StartupPhaseTiming(phase, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            RuntimeLog.Write("STARTUP", $"Phase '{phase}' canceled after {stopwatch.ElapsedMilliseconds} ms.");
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RuntimeLog.Write("STARTUP", $"Phase '{phase}' failed after {stopwatch.ElapsedMilliseconds} ms.", ex);
            throw;
        }
    }
}
