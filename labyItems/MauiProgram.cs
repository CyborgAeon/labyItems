using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using MigrationsLib.Migrations;

namespace labyItems;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		// Path for the app's DB used by migrations and runtime. For local testing this will be in AppData.
		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "default.db");
		Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? FileSystem.AppDataDirectory);

		// Initialize SQLite raw provider on platforms (ensures native libs are available)
		try
		{
			SQLitePCL.Batteries_V2.Init();
		}
		catch
		{
			// If Batteries init fails, migrations that require ADO.NET may still fail later; we catch to avoid startup crash here.
		}
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Register FluentMigrator runner to apply migrations against the app DB (use local file for testing)
		builder.Services.AddFluentMigratorCore()
			.ConfigureRunner(rb => rb
				.AddSQLite()
				.WithGlobalConnectionString($"Data Source={dbPath}")
				.ScanIn(typeof(MigrationsLib.Migrations.InitialMigration).Assembly).For.Migrations())
			.AddLogging(lb => {
				// FluentMigratorConsole uses Console APIs which may not be available on mobile platforms (Android/iOS/MacCatalyst).
				// Register the console logger only on platforms that support Console; otherwise use the Debug logger.
				if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS() && !OperatingSystem.IsMacCatalyst())
				{
					lb.AddFluentMigratorConsole();
				}
				else
				{
					lb.AddDebug();
				}
			});

		// Register default-db installer which will copy a packaged default.db on first-run (if present)
		builder.Services.AddSingleton<Services.IDefaultDatabaseInstaller, Services.DefaultDatabaseInstaller>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

var app = builder.Build();

// Ensure DB is present (copy packaged DB on first run) and then apply migrations
			using (var scope = app.Services.CreateScope())
			{
				var installer = scope.ServiceProvider.GetRequiredService<Services.IDefaultDatabaseInstaller>();
				try
				{
					installer.EnsureDatabaseAsync().GetAwaiter().GetResult();
				}
				catch (Exception ex)
				{
					var log = scope.ServiceProvider.GetRequiredService<ILogger<Services.DefaultDatabaseInstaller>>();
					log.LogError(ex, "Default DB installer failed");
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
					Services.LightweightMigrator.ApplyInitialSchema(dbPath, scope.ServiceProvider.GetService<ILogger>());
					log2.LogInformation("Lightweight migrations applied successfully.");
				}
				catch (Exception inner)
				{
					log2.LogError(inner, "Lightweight migrations also failed: {Message}", inner.Message);
				}
			}
		}

		return app;
	}
}