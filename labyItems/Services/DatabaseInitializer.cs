using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public interface IDatabaseInitializer
{
	Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class DatabaseInitializer : IDatabaseInitializer
{
	private readonly IServiceProvider _services;
	private readonly ILogger<DatabaseInitializer> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private bool _initialized;

	public DatabaseInitializer(IServiceProvider services, ILogger<DatabaseInitializer> logger)
	{
		_services = services;
		_logger = logger;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		if (_initialized) return;
		await _initLock.WaitAsync(cancellationToken);
		try
		{
			if (_initialized) return;
			await RunAsync(cancellationToken);
			_initialized = true;
		}
		finally
		{
			_initLock.Release();
		}
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		using var scope = _services.CreateScope();

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
		Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? FileSystem.AppDataDirectory);

		var installer = scope.ServiceProvider.GetRequiredService<IDefaultDatabaseInstaller>();
		try
		{
			await installer.EnsureDatabaseAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Default DB installer failed");
			// Do not rethrow - allow app to continue (migrations may still create DB)
		}

		var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
		var log2 = loggerFactory.CreateLogger("Migrations");
		try
		{
			var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
			runner.MigrateUp();
			log2.LogInformation("FluentMigrator applied migrations successfully.");
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
	}
}
