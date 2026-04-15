using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public interface IPackagedDatabaseSynchronizer
{
    Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default);
}

public sealed class PackagedDatabaseSynchronizer : IPackagedDatabaseSynchronizer
{
    private const string SeedVersion = "packaged-db";
    private readonly ILogger<PackagedDatabaseSynchronizer> _logger;

    public PackagedDatabaseSynchronizer(ILogger<PackagedDatabaseSynchronizer> logger)
    {
        _logger = logger;
    }

    public async Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            return;

        var tempPath = Path.Combine(FileSystem.CacheDirectory, "laby.pkg.db");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath) ?? FileSystem.CacheDirectory);

        var packagedChecksum = await CopyPackagedDatabaseToTempAsync(tempPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(packagedChecksum))
        {
            _logger.LogWarning("Skipped packaged DB sync: packaged laby.db is not available.");
            return;
        }

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(cancellationToken);
        EnsureSeedMetadataTable(conn);

        var existingChecksum = GetExistingChecksum(conn);
        if (string.Equals(existingChecksum, packagedChecksum, StringComparison.OrdinalIgnoreCase))
            return;

        using var packagedConn = new SqliteConnection($"Data Source={tempPath}");
        await packagedConn.OpenAsync(cancellationToken);

        var tables = GetTablesWithIsDefault(packagedConn).ToList();
        if (tables.Count == 0)
        {
            _logger.LogWarning("Skipped packaged DB sync: packaged DB contains no tables with is_default.");
            SaveChecksum(conn, tx: null, packagedChecksum);
            return;
        }

        using var tx = conn.BeginTransaction();
        foreach (var tableName in tables)
        {
            if (!TableExists(conn, tableName))
                continue;

            DeleteDefaultRows(conn, tx, tableName);
            CopyDefaultRows(conn, tx, packagedConn, tableName);
        }

        SaveChecksum(conn, tx, packagedChecksum);
        tx.Commit();

        InvalidateCaches();
        _logger.LogInformation("Packaged DB synchronized from updated package. Tables refreshed: {Tables}", string.Join(", ", tables));
    }

    private static async Task<string> CopyPackagedDatabaseToTempAsync(string tempPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("laby.db");
            using var output = File.Create(tempPath);

            using var sha256 = SHA256.Create();
            var buffer = new byte[81920];

            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                    break;

                output.Write(buffer, 0, read);
                sha256.TransformBlock(buffer, 0, read, null, 0);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            await output.FlushAsync(cancellationToken);
            return Convert.ToHexString(sha256.Hash);
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
        catch (Exception ex)
        {
            // If packaged DB cannot be read, do not block startup.
            Console.WriteLine($"Packaged DB sync failed: {ex.Message}");
            return string.Empty;
        }
    }

    private static IEnumerable<string> GetTablesWithIsDefault(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var tableName = reader.GetString(0);
            if (TableHasIsDefault(conn, tableName))
                yield return tableName;
        }
    }

    private static bool TableHasIsDefault(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({EscapeIdentifier(tableName)});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals("is_default", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void DeleteDefaultRows(SqliteConnection conn, SqliteTransaction tx, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"DELETE FROM {EscapeIdentifier(tableName)} WHERE is_default = 1;";
        cmd.ExecuteNonQuery();
    }

    private static void CopyDefaultRows(SqliteConnection conn, SqliteTransaction tx, SqliteConnection packagedConn, string tableName)
    {
        var columns = GetTableColumns(packagedConn, tableName).ToList();
        if (columns.Count == 0)
            return;

        using var attach = conn.CreateCommand();
        attach.Transaction = tx;
        attach.CommandText = "ATTACH DATABASE $source AS packaged;";
        attach.Parameters.AddWithValue("$source", packagedConn.DataSource);
        attach.ExecuteNonQuery();

        try
        {
            var columnList = string.Join(", ", columns.Select(EscapeIdentifier));
            using var insert = conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = $"INSERT OR IGNORE INTO {EscapeIdentifier(tableName)} ({columnList}) SELECT {columnList} FROM packaged.{EscapeIdentifier(tableName)} WHERE is_default = 1;";
            insert.ExecuteNonQuery();
        }
        finally
        {
            using var detach = conn.CreateCommand();
            detach.Transaction = tx;
            detach.CommandText = "DETACH DATABASE packaged;";
            detach.ExecuteNonQuery();
        }
    }

    private static IEnumerable<string> GetTableColumns(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({EscapeIdentifier(tableName)});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            yield return reader.GetString(1);
    }

    private static bool TableExists(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        cmd.Parameters.AddWithValue("$tableName", tableName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static void EnsureSeedMetadataTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS seed_metadata (
  seed_version TEXT,
  schema_version INTEGER,
  build_id TEXT,
  checksum TEXT,
  created_at TEXT
);";
        cmd.ExecuteNonQuery();
    }

    private static void SaveChecksum(SqliteConnection conn, SqliteTransaction? tx, string checksum)
    {
        using var delete = conn.CreateCommand();
        if (tx != null)
            delete.Transaction = tx;
        delete.CommandText = "DELETE FROM seed_metadata WHERE seed_version = $seedVersion;";
        delete.Parameters.AddWithValue("$seedVersion", SeedVersion);
        delete.ExecuteNonQuery();

        using var insert = conn.CreateCommand();
        if (tx != null)
            insert.Transaction = tx;
        insert.CommandText = @"
INSERT INTO seed_metadata (seed_version, schema_version, build_id, checksum, created_at)
VALUES ($seedVersion, $schemaVersion, $buildId, $checksum, $createdAt);";
        insert.Parameters.AddWithValue("$seedVersion", SeedVersion);
        insert.Parameters.AddWithValue("$schemaVersion", 1);
        insert.Parameters.AddWithValue("$buildId", "packaged-db-sync");
        insert.Parameters.AddWithValue("$checksum", checksum);
        insert.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));
        insert.ExecuteNonQuery();
    }

    private static string? GetExistingChecksum(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT checksum
FROM seed_metadata
WHERE seed_version = $seedVersion
ORDER BY rowid DESC
LIMIT 1;";
        cmd.Parameters.AddWithValue("$seedVersion", SeedVersion);
        var scalar = cmd.ExecuteScalar();
        return scalar?.ToString();
    }

    private static void InvalidateCaches()
    {
        EvolutionService.InvalidateCache();
        AbilityDefinitionLookupService.InvalidateCache();
        AbilityDetailsLookupService.InvalidateCache();
        SpellService.InvalidateCache();
        MiracleService.InvalidateCache();
        DruidEvocationService.InvalidateCache();
        ClassService.InvalidateCache();
        PeopleService.InvalidateCache();
        LifeScalesService.InvalidateCache();
    }

    private static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier must be provided.", nameof(identifier));

        return '"' + identifier.Replace("\"", "\"\"") + '"';
    }
}
