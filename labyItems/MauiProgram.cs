using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using MigrationsLib.Migrations;
using labyItems.Services;

namespace labyItems;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		// Path for the app's DB used by migrations and runtime. For local testing this will be in AppData.
		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
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
				fonts.AddFont("fa-solid-900.ttf", "FASolid");
			});

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

		// Register default-db installer which will copy a packaged laby.db on first-run (if present)
		builder.Services.AddSingleton<Services.IDefaultDatabaseInstaller, Services.DefaultDatabaseInstaller>();
		builder.Services.AddSingleton<Services.IDatabaseInitializer, Services.DatabaseInitializer>();
		builder.Services.AddSingleton<IBattleboardExportService, BattleboardExportService>();
		builder.Services.AddSingleton<IFileService, MauiFileService>();
		builder.Services.AddSingleton<IClipboardService, MauiClipboardService>();
		builder.Services.AddSingleton<ILauncherService, MauiLauncherService>();
		builder.Services.AddSingleton<IShareService, MauiShareService>();
		builder.Services.AddSingleton<IExportService, ExportService>();
		builder.Services.AddSingleton<ICharacterAdvancementDomainService, CharacterAdvancementDomainService>();
		builder.Services.AddSingleton<IAdvancementTabVisibilityService, AdvancementTabVisibilityService>();
		builder.Services.AddSingleton<IAdvancementValidationService, AdvancementValidationService>();
		builder.Services.AddSingleton<ICharacterCreationDataService, CharacterCreationDataService>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
