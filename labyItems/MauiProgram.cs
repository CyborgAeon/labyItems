using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using MigrationsLib.Migrations;
using CommunityToolkit.Maui;

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
			})
				   .UseMauiCommunityToolkit();

		// Register FluentMigrator runner to apply migrations against the app DB (use local file for testing)
		builder.Services.AddFluentMigratorCore()
			.ConfigureRunner(rb => rb
				.AddSQLite()
				.WithGlobalConnectionString($"Data Source={dbPath}")
				.ScanIn(typeof(MigrationsLib.Migrations.InitialMigration).Assembly).For.Migrations())
			.AddLogging(lb =>
			{
				lb.ClearProviders();
				lb.AddDebug();
			});

		// Register default-db installer which will copy a packaged default.db on first-run (if present)
		builder.Services.AddSingleton<Services.IDefaultDatabaseInstaller, Services.DefaultDatabaseInstaller>();
		builder.Services.AddSingleton<Services.IDatabaseInitializer, Services.DatabaseInitializer>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
