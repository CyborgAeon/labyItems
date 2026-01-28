using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;
using SQLite;

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

			var packagedTempPath = dbPath + ".pkg";
			SeedMeta? packagedMeta = null;
			var hasPackaged = false;
			try
			{
				using var packagedStream = await FileSystem.OpenAppPackageFileAsync(DbFileName);
				using (var outFs = File.OpenWrite(packagedTempPath))
				{
					await packagedStream.CopyToAsync(outFs);
				}

				packagedMeta = TryReadSeedMeta(packagedTempPath);
				hasPackaged = true;
			}
			catch (FileNotFoundException)
			{
				hasPackaged = false;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while staging packaged {DbFile}", DbFileName);
			}

			if (File.Exists(dbPath))
			{
				if (hasPackaged && packagedMeta != null)
				{
					var currentMeta = TryReadSeedMeta(dbPath);
					if (currentMeta == null || IsPackagedNewer(packagedMeta, currentMeta))
					{
						try
						{
							File.Move(packagedTempPath, dbPath, true);
							_logger.LogInformation("Replaced {Path} with newer packaged {DbFile}", dbPath, DbFileName);
							return;
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "Failed to replace {Path} with packaged {DbFile}", dbPath, DbFileName);
						}
					}
				}

				CleanupTemp(packagedTempPath);
				_logger.LogInformation("Database already exists at {Path}", dbPath);
				return;
			}

			_logger.LogInformation("Database not found at {Path}. Attempting to copy packaged {DbFile} if present.", dbPath, DbFileName);
			try
			{
				if (hasPackaged && File.Exists(packagedTempPath))
				{
					File.Move(packagedTempPath, dbPath, true);
					_logger.LogInformation("Copied packaged {DbFile} to {Path}", DbFileName, dbPath);
				}
			}
			catch (FileNotFoundException)
			{
				// No packaged DB present; migrations will create the DB file when we run them.
				_logger.LogInformation("Packaged {DbFile} not found; will rely on migrations to create the database.", DbFileName);
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

		private static SeedMeta? TryReadSeedMeta(string path)
		{
			try
			{
				using var conn = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
				var rows = conn.Query<SeedRow>("SELECT seed_version, schema_version FROM seed_metadata ORDER BY rowid DESC LIMIT 1;");
				if (rows.Count == 0)
					return null;

				var row = rows[0];
				return new SeedMeta(ParseSeedVersion(row.seed_version), row.schema_version ?? 0);
			}
			catch
			{
				return null;
			}
		}

		private static bool IsPackagedNewer(SeedMeta packaged, SeedMeta current)
		{
			if (packaged.SchemaVersion > current.SchemaVersion)
				return true;
			if (packaged.SchemaVersion < current.SchemaVersion)
				return false;
			return packaged.SeedVersion > current.SeedVersion;
		}

		private static long ParseSeedVersion(string? value)
			=> long.TryParse(value, out var parsed) ? parsed : 0;

		private sealed record SeedMeta(long SeedVersion, long SchemaVersion);

		private sealed class SeedRow
		{
			public string? seed_version { get; set; }
			public long? schema_version { get; set; }
		}
	}
}
