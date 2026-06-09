using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#if !IOS
using FluentMigrator.Runner;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using labyItems.Helpers;

namespace labyItems.Services;

public interface IDatabaseInitializer
{
	Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class DatabaseInitializer : IDatabaseInitializer
{
	private readonly IServiceProvider _services;
	private readonly ILogger<DatabaseInitializer> _logger;
    private readonly IEvolutionDataSynchronizer _evolutionDataSynchronizer;
    private readonly IAbilityDefinitionDataSynchronizer _abilityDefinitionDataSynchronizer;
    private readonly ICharacterReferenceDataSynchronizer _characterReferenceDataSynchronizer;
    private readonly IPackagedDatabaseSynchronizer _packagedDatabaseSynchronizer;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private bool _initialized;

	public DatabaseInitializer(
        IServiceProvider services,
        ILogger<DatabaseInitializer> logger,
        IEvolutionDataSynchronizer evolutionDataSynchronizer,
        IAbilityDefinitionDataSynchronizer abilityDefinitionDataSynchronizer,
        ICharacterReferenceDataSynchronizer characterReferenceDataSynchronizer,
        IPackagedDatabaseSynchronizer packagedDatabaseSynchronizer)
	{
		_services = services;
		_logger = logger;
        _evolutionDataSynchronizer = evolutionDataSynchronizer;
        _abilityDefinitionDataSynchronizer = abilityDefinitionDataSynchronizer;
        _characterReferenceDataSynchronizer = characterReferenceDataSynchronizer;
        _packagedDatabaseSynchronizer = packagedDatabaseSynchronizer;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		if (_initialized) return;
		await _initLock.WaitAsync(cancellationToken);
		try
		{
			if (_initialized) return;
			await Task.Run(() => RunAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
			_initialized = true;
		}
		finally
		{
			_initLock.Release();
		}
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		var total = Stopwatch.StartNew();
		LogPhaseBoundary("Database initializer", "started");

		using var scope = _services.CreateScope();

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
		Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? FileSystem.AppDataDirectory);

		var installer = scope.ServiceProvider.GetRequiredService<IDefaultDatabaseInstaller>();
		try
		{
			var installTimer = Stopwatch.StartNew();
			LogPhaseBoundary("Database install phase", "started");
			await installer.EnsureDatabaseAsync().ConfigureAwait(false);
			installTimer.Stop();
			LogPhaseBoundary("Database install phase", $"completed in {installTimer.ElapsedMilliseconds} ms");
		}
		catch (Exception ex)
		{
			RuntimeLog.Write("STARTUP", "Database install phase failed.", ex);
			_logger.LogError(ex, "Default DB installer failed");
			// Do not rethrow - allow app to continue (migrations may still create DB)
		}

		var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
		var log2 = loggerFactory.CreateLogger("Migrations");
#if IOS || ANDROID
		try
		{
			var migrationTimer = Stopwatch.StartNew();
			LogPhaseBoundary("Migration phase", "started (lightweight mobile migrator)");
			LightweightMigrator.ApplyInitialSchema(dbPath, scope.ServiceProvider.GetService<ILogger>());
			log2.LogInformation("Applied lightweight migrations on mobile platform.");
			migrationTimer.Stop();
			LogPhaseBoundary("Migration phase", $"completed in {migrationTimer.ElapsedMilliseconds} ms");
		}
		catch (Exception ex)
		{
			RuntimeLog.Write("STARTUP", "Migration phase failed.", ex);
			log2.LogError(ex, "Lightweight migrations failed on mobile platform: {Message}", ex.Message);
		}
#else
		try
		{
			var migrationTimer = Stopwatch.StartNew();
			RuntimeLog.Write("STARTUP", "Migration phase started (FluentMigrator).");
			var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
			runner.MigrateUp();
			log2.LogInformation("FluentMigrator applied migrations successfully.");
			migrationTimer.Stop();
			RuntimeLog.Write("STARTUP", $"Migration phase completed in {migrationTimer.ElapsedMilliseconds} ms.");
		}
		catch (Exception ex)
		{
			// FluentMigrator couldn't run (likely missing ADO.NET provider on this platform). Fall back to lightweight SQL-based schema application.
			log2.LogWarning(ex, "FluentMigrator failed to run (platform/provider issue). Falling back to lightweight schema runner.");
			try
			{
				LightweightMigrator.ApplyInitialSchema(dbPath, scope.ServiceProvider.GetService<ILogger>());
				log2.LogInformation("Lightweight migrations applied successfully.");
			}
			catch (Exception inner)
			{
				log2.LogError(inner, "Lightweight migrations also failed: {Message}", inner.Message);
			}
		}
#endif

        try
        {
			var syncTimer = Stopwatch.StartNew();
			LogPhaseBoundary("Packaged DB sync phase", "started");
	            await _packagedDatabaseSynchronizer.EnsureCurrentAsync(dbPath, cancellationToken).ConfigureAwait(false);
            log2.LogInformation("Packaged database defaults are synchronized.");
			syncTimer.Stop();
			LogPhaseBoundary("Packaged DB sync phase", $"completed in {syncTimer.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
			RuntimeLog.Write("STARTUP", "Packaged DB sync phase failed.", ex);
            log2.LogError(ex, "Packaged database sync failed: {Message}", ex.Message);
        }

        try
        {
			var evolutionTimer = Stopwatch.StartNew();
			LogPhaseBoundary("Evolution data sync phase", "started");
	            await _evolutionDataSynchronizer.EnsureCurrentAsync(dbPath, cancellationToken).ConfigureAwait(false);
            log2.LogInformation("Evolution defaults are synchronized.");
			evolutionTimer.Stop();
			LogPhaseBoundary("Evolution data sync phase", $"completed in {evolutionTimer.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
			RuntimeLog.Write("STARTUP", "Evolution data sync phase failed.", ex);
            log2.LogError(ex, "Evolution default sync failed: {Message}", ex.Message);
        }

        try
        {
			var abilityTimer = Stopwatch.StartNew();
			LogPhaseBoundary("Ability definition sync phase", "started");
	            await _abilityDefinitionDataSynchronizer.EnsureCurrentAsync(dbPath, cancellationToken).ConfigureAwait(false);
            log2.LogInformation("Ability definition defaults are synchronized.");
			abilityTimer.Stop();
			LogPhaseBoundary("Ability definition sync phase", $"completed in {abilityTimer.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
			RuntimeLog.Write("STARTUP", "Ability definition sync phase failed.", ex);
            log2.LogError(ex, "Ability definition sync failed: {Message}", ex.Message);
        }

        try
        {
			var referenceTimer = Stopwatch.StartNew();
			LogPhaseBoundary("Character reference sync phase", "started");
	            await _characterReferenceDataSynchronizer.EnsureCurrentAsync(dbPath, cancellationToken).ConfigureAwait(false);
            log2.LogInformation("Character reference defaults are synchronized.");
			referenceTimer.Stop();
			LogPhaseBoundary("Character reference sync phase", $"completed in {referenceTimer.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
			RuntimeLog.Write("STARTUP", "Character reference sync phase failed.", ex);
            log2.LogError(ex, "Character reference sync failed: {Message}", ex.Message);
        }

		total.Stop();
		LogPhaseBoundary("Database initializer", $"finished in {total.ElapsedMilliseconds} ms");
	}

	private static void LogPhaseBoundary(string phase, string state)
	{
		var threadId = Environment.CurrentManagedThreadId;
		var onMainThread = MainThread.IsMainThread;
		RuntimeLog.Write("STARTUP", $"{phase} {state}. threadId={threadId} mainThread={onMainThread}.");

		if (!onMainThread)
			return;

		var message = $"Non-UI startup phase '{phase}' is executing on UI threadId={threadId}.";
		RuntimeLog.Write("STARTUP", message);
		Debug.Assert(false, message);
	}
}
