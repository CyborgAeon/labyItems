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
		private const string DbFileName = "laby.db";

		public DefaultDatabaseInstaller(ILogger<DefaultDatabaseInstaller> logger)
		{
			_logger = logger;
		}

		public async Task EnsureDatabaseAsync()
		{
			var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbFileName);
			var legacyDb1 = Path.Combine(FileSystem.AppDataDirectory, "default.db");
			var legacyDb2 = Path.Combine(FileSystem.AppDataDirectory, "evocs.db");
			TryDeleteLegacy(legacyDb1);
			TryDeleteLegacy(legacyDb2);

			if (File.Exists(dbPath))
			{
				EnsureWritableFile(dbPath);
				_logger.LogInformation("Database already exists at {Path}. Preserving existing user database.", dbPath);
				return;
			}

			var packagedTempPath = dbPath + ".pkg";
			var hasPackaged = false;
			try
			{
				using var packagedStream = await FileSystem.OpenAppPackageFileAsync(DbFileName);
				using (var outFs = File.OpenWrite(packagedTempPath))
				{
					await packagedStream.CopyToAsync(outFs);
				}
				hasPackaged = true;
			}
			catch (FileNotFoundException)
			{
				_logger.LogInformation("Packaged {DbFile} not found; will rely on migrations to create the database.", DbFileName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while copying packaged {DbFile}", DbFileName);
				throw;
			}

			_logger.LogInformation("Database not found at {Path}. Attempting to copy packaged {DbFile} if present.", dbPath, DbFileName);
			try
			{
				if (hasPackaged && File.Exists(packagedTempPath))
				{
					File.Move(packagedTempPath, dbPath, true);
					EnsureWritableFile(dbPath);
					_logger.LogInformation("Copied packaged {DbFile} to {Path}", DbFileName, dbPath);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while copying packaged {DbFile}", DbFileName);
				throw;
			}
			finally
			{
				CleanupTemp(packagedTempPath);
			}
		}

		private void TryDeleteLegacy(string path)
		{
			try
			{
				if (File.Exists(path))
					File.Delete(path);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to delete legacy db at {Path}", path);
			}
		}

		private static void CleanupTemp(string path)
		{
			try
			{
				if (File.Exists(path))
					File.Delete(path);
			}
			catch
			{
				// best effort
			}
		}

		private void EnsureWritableFile(string path)
		{
			try
			{
				if (!File.Exists(path))
					return;

				var attributes = File.GetAttributes(path);
				if ((attributes & FileAttributes.ReadOnly) != 0)
					File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to clear readonly attribute for database file {Path}", path);
			}

			try
			{
				using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Database file {Path} is not writable after attribute check. Rewriting file.", path);
				RewriteWritable(path);
			}
		}

		private void RewriteWritable(string path)
		{
			var tempPath = path + ".rw";
			try
			{
				if (!File.Exists(path))
					return;

				using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var target = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
				{
					source.CopyTo(target);
				}

				File.Move(tempPath, path, true);
			}
			catch
			{
				_logger.LogWarning("Failed to rewrite writable database copy for {Path}", path);
			}
			finally
			{
				CleanupTemp(tempPath);
			}
		}

	}
}
