using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Services;

public interface ICharacterReferenceDataSynchronizer
{
    Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default);
}

public sealed class CharacterReferenceDataSynchronizer : ICharacterReferenceDataSynchronizer
{
    private const string SeedVersion = "character-reference-defaults-v1";

    private readonly ILogger<CharacterReferenceDataSynchronizer> _logger;
    private readonly IFileService _fileService;

    public CharacterReferenceDataSynchronizer(
        ILogger<CharacterReferenceDataSynchronizer> logger,
        IFileService fileService)
    {
        _logger = logger;
        _fileService = fileService;
    }

    public async Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            return;

        var buildId = GetPackagedDataBuildId();

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        EnsureSeedMetadataTable(conn);

        var existingChecksum = GetExistingChecksum(conn);
        var existingBuildId = GetExistingBuildId(conn);
        if (!string.IsNullOrWhiteSpace(existingChecksum)
            && string.Equals(existingBuildId, buildId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipped character reference sync: packaged build id '{BuildId}' already applied.", buildId);
            return;
        }

        var classesJson = await TryReadAssetTextAsync("people/classes.json").ConfigureAwait(false);
        var racesJson = await TryReadAssetTextAsync("people/people.json").ConfigureAwait(false);
        var lifeScalesJson = await TryReadAssetTextAsync("people/lifescales.json").ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(classesJson)
            || string.IsNullOrWhiteSpace(racesJson)
            || string.IsNullOrWhiteSpace(lifeScalesJson))
        {
            _logger.LogWarning("Skipped character reference sync: one or more packaged people JSON assets could not be loaded.");
            return;
        }

        var classes = ParseNamedJsonObjects(classesJson);
        var races = ParseNamedJsonObjects(racesJson);
        var lifeScaleRows = ParseLifeScaleRows(lifeScalesJson);

        if (classes.Count == 0 || races.Count == 0 || lifeScaleRows.Count == 0)
        {
            _logger.LogWarning("Skipped character reference sync: packaged people JSON parsed to no data.");
            return;
        }

        var checksum = ComputeChecksum($"{classesJson}\n{racesJson}\n{lifeScalesJson}");

        if (string.Equals(existingChecksum, checksum, StringComparison.OrdinalIgnoreCase))
        {
            SaveChecksum(conn, tx: null, checksum, buildId);
            return;
        }

        using var tx = conn.BeginTransaction();
        SyncNamedJsonTable(conn, tx, "classes", "class", classes);
        SyncNamedJsonTable(conn, tx, "races", "race", races);
        SyncLifeScales(conn, tx, lifeScaleRows, classes.Keys, races.Keys);
        SaveChecksum(conn, tx, checksum, buildId);
        tx.Commit();

        ClassService.InvalidateCache();
        PeopleService.InvalidateCache();
        LifeScalesService.InvalidateCache();

        _logger.LogInformation(
            "Character reference defaults synchronized. Classes: {ClassCount}, races: {RaceCount}, lifescales: {LifeScaleCount}.",
            classes.Count,
            races.Count,
            lifeScaleRows.Count);
    }

    private async Task<string> TryReadAssetTextAsync(string relativePath)
    {
        try
        {
            return await _fileService.ReadPackageTextAsync(relativePath);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Dictionary<string, string> ParseNamedJsonObjects(string rawJson)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var name = (property.Name ?? string.Empty).Trim();
                if (name.Length == 0 || property.Value.ValueKind != JsonValueKind.Object)
                    continue;

                result[name] = property.Value.GetRawText();
            }
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    private static List<LifeScaleSeedRow> ParseLifeScaleRows(string rawJson)
    {
        var rows = new List<LifeScaleSeedRow>();

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return rows;

            foreach (var raceProperty in doc.RootElement.EnumerateObject())
            {
                var raceName = (raceProperty.Name ?? string.Empty).Trim();
                if (raceName.Length == 0 || raceProperty.Value.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var classProperty in raceProperty.Value.EnumerateObject())
                {
                    var className = (classProperty.Name ?? string.Empty).Trim();
                    if (className.Length == 0 || classProperty.Value.ValueKind != JsonValueKind.Array)
                        continue;

                    var index = 1;
                    foreach (var pair in classProperty.Value.EnumerateArray())
                    {
                        if (pair.ValueKind != JsonValueKind.Array)
                        {
                            index++;
                            continue;
                        }

                        var values = pair.EnumerateArray().ToArray();
                        if (values.Length < 2
                            || values[0].ValueKind != JsonValueKind.Number
                            || values[1].ValueKind != JsonValueKind.Number)
                        {
                            index++;
                            continue;
                        }

                        rows.Add(new LifeScaleSeedRow(
                            Race: raceName,
                            Class: className,
                            Index: index,
                            Body: values[0].GetInt32(),
                            Loc: values[1].GetInt32()));
                        index++;
                    }
                }
            }
        }
        catch
        {
            return new List<LifeScaleSeedRow>();
        }

        return rows;
    }

    private static void SyncNamedJsonTable(
        SqliteConnection conn,
        SqliteTransaction tx,
        string tableName,
        string idPrefix,
        IReadOnlyDictionary<string, string> packagedRows)
    {
        if (!TableExists(conn, tableName))
            return;

        var existingRows = LoadNamedRows(conn, tx, tableName);
        var packagedNames = new HashSet<string>(packagedRows.Keys.Select(NormalizeName), StringComparer.OrdinalIgnoreCase);
        var retainedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var duplicate in existingRows
                     .Where(row => !row.NonStandard)
                     .GroupBy(row => NormalizeName(row.Name), StringComparer.OrdinalIgnoreCase)
                     .SelectMany(group => group.Skip(1)))
        {
            DeleteById(conn, tx, tableName, duplicate.Id);
        }

        foreach (var (name, json) in packagedRows)
        {
            var normalizedName = NormalizeName(name);
            var existing = existingRows.FirstOrDefault(row =>
                !row.NonStandard && NormalizeName(row.Name).Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

            var id = existing?.Id;
            if (string.IsNullOrWhiteSpace(id))
                id = DeterministicGuid($"{idPrefix}|{name}");

            UpsertNamedRow(conn, tx, tableName, id, name, json);
            retainedIds.Add(id);
        }

        foreach (var obsolete in existingRows.Where(row =>
                     !row.NonStandard
                     && !packagedNames.Contains(NormalizeName(row.Name))
                     && !retainedIds.Contains(row.Id)))
        {
            DeleteById(conn, tx, tableName, obsolete.Id);
        }
    }

    private static void SyncLifeScales(
        SqliteConnection conn,
        SqliteTransaction tx,
        IReadOnlyList<LifeScaleSeedRow> packagedRows,
        IEnumerable<string> standardClassNames,
        IEnumerable<string> standardRaceNames)
    {
        if (!TableExists(conn, "lifescales"))
            return;

        var classNames = standardClassNames
            .Select(NormalizeName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var raceNames = standardRaceNames
            .Select(NormalizeName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (classNames.Count == 0 || raceNames.Count == 0)
            return;

        using (var delete = conn.CreateCommand())
        {
            delete.Transaction = tx;
            var raceParams = raceNames.Select((_, index) => $"$race{index}").ToList();
            var classParams = classNames.Select((_, index) => $"$class{index}").ToList();
            delete.CommandText = $@"
DELETE FROM lifescales
WHERE lower(race) IN ({string.Join(", ", raceParams)})
  AND lower(""class"") IN ({string.Join(", ", classParams)});";

            for (var index = 0; index < raceNames.Count; index++)
                delete.Parameters.AddWithValue(raceParams[index], raceNames[index]);

            for (var index = 0; index < classNames.Count; index++)
                delete.Parameters.AddWithValue(classParams[index], classNames[index]);

            delete.ExecuteNonQuery();
        }

        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = @"
    INSERT OR REPLACE INTO lifescales (race, ""class"", idx, body, loc)
VALUES ($race, $class, $idx, $body, $loc);";

        var raceParam = insert.Parameters.Add("$race", SqliteType.Text);
        var classParam = insert.Parameters.Add("$class", SqliteType.Text);
        var idxParam = insert.Parameters.Add("$idx", SqliteType.Integer);
        var bodyParam = insert.Parameters.Add("$body", SqliteType.Integer);
        var locParam = insert.Parameters.Add("$loc", SqliteType.Integer);

        var dedupedRows = packagedRows
            .Where(row => row.Index > 0)
            .GroupBy(
                row => $"{NormalizeName(row.Race)}|{NormalizeName(row.Class)}|{row.Index}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        foreach (var row in dedupedRows)
        {
            raceParam.Value = row.Race;
            classParam.Value = row.Class;
            idxParam.Value = row.Index;
            bodyParam.Value = row.Body;
            locParam.Value = row.Loc;
            insert.ExecuteNonQuery();
        }
    }

    private static List<NamedJsonRow> LoadNamedRows(SqliteConnection conn, SqliteTransaction tx, string tableName)
    {
        var rows = new List<NamedJsonRow>();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT id, name, data_json FROM {EscapeIdentifier(tableName)};";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var dataJson = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            rows.Add(new NamedJsonRow(id, name, dataJson, TryParseNonStandardFlag(dataJson)));
        }

        return rows;
    }

    private static void UpsertNamedRow(
        SqliteConnection conn,
        SqliteTransaction tx,
        string tableName,
        string id,
        string name,
        string dataJson)
    {
        var now = DateTime.UtcNow.ToString("o");
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $@"
INSERT INTO {EscapeIdentifier(tableName)} (id, name, name_lower, data_json, created_at, updated_at)
VALUES ($id, $name, $nameLower, $dataJson, $createdAt, $updatedAt)
ON CONFLICT(id) DO UPDATE SET
    name = excluded.name,
    name_lower = excluded.name_lower,
    data_json = excluded.data_json,
    updated_at = excluded.updated_at;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$nameLower", NormalizeName(name));
        cmd.Parameters.AddWithValue("$dataJson", dataJson);
        cmd.Parameters.AddWithValue("$createdAt", now);
        cmd.Parameters.AddWithValue("$updatedAt", now);
        cmd.ExecuteNonQuery();
    }

    private static void DeleteById(SqliteConnection conn, SqliteTransaction tx, string tableName, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"DELETE FROM {EscapeIdentifier(tableName)} WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static bool TryParseNonStandardFlag(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (TryReadBool(doc.RootElement, "NonStandard", out var upper))
                return upper;

            if (TryReadBool(doc.RootElement, "nonStandard", out var lower))
                return lower;
        }
        catch
        {
        }

        return false;
    }

    private static bool TryReadBool(JsonElement root, string propertyName, out bool value)
    {
        value = false;
        if (!root.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            value = false;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String
            && bool.TryParse(property.GetString(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
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
        return cmd.ExecuteScalar()?.ToString();
    }

    private static void SaveChecksum(SqliteConnection conn, SqliteTransaction? tx, string checksum, string buildId)
    {
        using (var delete = conn.CreateCommand())
        {
            if (tx != null)
                delete.Transaction = tx;
            delete.CommandText = "DELETE FROM seed_metadata WHERE seed_version = $seedVersion;";
            delete.Parameters.AddWithValue("$seedVersion", SeedVersion);
            delete.ExecuteNonQuery();
        }

        using var insert = conn.CreateCommand();
        if (tx != null)
            insert.Transaction = tx;
        insert.CommandText = @"
INSERT INTO seed_metadata (seed_version, schema_version, build_id, checksum, created_at)
VALUES ($seedVersion, $schemaVersion, $buildId, $checksum, $createdAt);";
        insert.Parameters.AddWithValue("$seedVersion", SeedVersion);
        insert.Parameters.AddWithValue("$schemaVersion", 1);
        insert.Parameters.AddWithValue("$buildId", buildId);
        insert.Parameters.AddWithValue("$checksum", checksum);
        insert.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));
        insert.ExecuteNonQuery();
    }

    private static string? GetExistingBuildId(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT build_id
FROM seed_metadata
WHERE seed_version = $seedVersion
ORDER BY rowid DESC
LIMIT 1;";
        cmd.Parameters.AddWithValue("$seedVersion", SeedVersion);
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string GetPackagedDataBuildId()
        => $"{AppInfo.Current.VersionString}+{AppInfo.Current.BuildString}";

    private static string ComputeChecksum(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        return Convert.ToHexString(sha256.ComputeHash(bytes));
    }

    private static string DeterministicGuid(string input)
    {
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes((input ?? string.Empty).ToLowerInvariant()));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes).ToString();
    }

    private static string NormalizeName(string? name)
        => (name ?? string.Empty).Trim().ToLowerInvariant();

    private static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier must be provided.", nameof(identifier));

        return '"' + identifier.Replace("\"", "\"\"") + '"';
    }

    private sealed record NamedJsonRow(string Id, string Name, string DataJson, bool NonStandard);

    private sealed record LifeScaleSeedRow(string Race, string Class, int Index, int Body, int Loc);
}
