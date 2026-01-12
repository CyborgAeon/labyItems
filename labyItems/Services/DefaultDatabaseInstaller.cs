using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;

namespace labyItems.Services
{
	public interface IDefaultDatabaseInstaller
	{
		Task EnsureDatabaseAsync();
	}

	public class DefaultDatabaseInstaller : IDefaultDatabaseInstaller
	{
		private readonly ILogger<DefaultDatabaseInstaller> _logger;

		public DefaultDatabaseInstaller(ILogger<DefaultDatabaseInstaller> logger)
		{
			_logger = logger;
		}

		public async Task EnsureDatabaseAsync()
		{
			var dbPath = Path.Combine(FileSystem.AppDataDirectory, "default.db");
			if (File.Exists(dbPath))
			{
				_logger.LogInformation("Database already exists at {Path}", dbPath);
				return;
			}

			_logger.LogInformation("Database not found at {Path}. Attempting to copy packaged default.db if present.", dbPath);
			try
			{
				// Try to open the packaged default.db (if you've included one in the app package)
				using var packagedStream = await FileSystem.OpenAppPackageFileAsync("default.db");
				var tempPath = dbPath + ".tmp";
				using (var outFs = File.OpenWrite(tempPath))
				{
					await packagedStream.CopyToAsync(outFs);
				}

				// Move into place atomically
				File.Move(tempPath, dbPath);
				_logger.LogInformation("Copied packaged default.db to {Path}", dbPath);
			}
			catch (FileNotFoundException)
			{
				// No packaged DB present; migrations will create the DB file when we run them.
				_logger.LogInformation("Packaged default.db not found; will rely on migrations to create the database.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while copying packaged default.db");
				throw;
			}
		}
	}
}
