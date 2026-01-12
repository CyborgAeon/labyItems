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
			.AddLogging(lb => lb.AddFluentMigratorConsole());

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

				var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
				runner.MigrateUp();
			}

			return app;
	}
}
