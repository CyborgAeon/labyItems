using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace labyItems.Services;

/// <summary>Repairs missing packaged catalogue rows without replacing user content.</summary>
public sealed class ReferenceCatalogueDataSynchronizer : IReferenceCatalogueDataSynchronizer
{
    private readonly IFileService _files;
    private readonly ILogger<ReferenceCatalogueDataSynchronizer> _logger;
    private static readonly Catalogue[] Catalogues =
    [
        new("spells", "grimoire/allSpells.json", "level", ["colour"],
            ["level INTEGER", "colour TEXT", "range TEXT", "duration TEXT", "verbal TEXT", "is_advanced INTEGER"]),
        new("miracles", "words_from_above/miracles.json", "power", ["sphere", "alignment"],
            ["power INTEGER", "sphere TEXT", "alignment TEXT", "is_advanced INTEGER"]),
        new("evocs", "druids_way/evocs.json", "power", ["fields_json"],
            ["power INTEGER", "range TEXT", "duration TEXT", "verbal TEXT", "fields_json TEXT", "is_advanced INTEGER", "is_default INTEGER"]),
        new("neuronics", "o-insight/o_insight_neuronics.json", "power", ["type"],
            ["power INTEGER", "type TEXT", "range TEXT", "duration TEXT", "immunities TEXT", "notes TEXT", "as_per TEXT", "todo TEXT", "damage_json TEXT", "is_default INTEGER"])
    ];

    public ReferenceCatalogueDataSynchronizer(IFileService files, ILogger<ReferenceCatalogueDataSynchronizer> logger)
    {
        _files = files;
        _logger = logger;
    }

    public async Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        // Read and validate every asset before touching the database. A packaging
        // failure must not leave startup with a partially seeded catalogue.
        var seeds = new List<(Catalogue Catalogue, List<Dictionary<string, object?>> Rows)>();
        foreach (var catalogue in Catalogues)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await _files.ReadPackageTextAsync(catalogue.Asset).ConfigureAwait(false);
            seeds.Add((catalogue, ParseRows(catalogue, json)));
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        var inserted = 0;
        foreach (var (catalogue, rows) in seeds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureTable(connection, transaction, catalogue);
            var existingKeys = ReadExistingKeys(connection, transaction, catalogue);
            var added = 0;
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = Identity(catalogue, row);
                if (!existingKeys.Add(key))
                    continue;
                added += InsertRow(connection, transaction, catalogue, row, key);
            }
            if (catalogue.Table == "evocs" && added > 0)
                RebuildEvocationSearchIndexes(connection, transaction, cancellationToken);
            inserted += added;
        }
        transaction.Commit();
        if (inserted == 0)
            return;
        SpellService.InvalidateCache();
        MiracleService.InvalidateCache();
        DruidEvocationService.InvalidateCache();
        NeuronicService.InvalidateCache();
        _logger.LogInformation("Restored {Count} missing packaged reference catalogue rows.", inserted);
    }

    private static List<Dictionary<string, object?>> ParseRows(Catalogue catalogue, string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Packaged catalogue {catalogue.Asset} must be an array.");
        var rows = new List<Dictionary<string, object?>>();
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var name = Text(entry, "name").Trim();
            if (entry.ValueKind != JsonValueKind.Object || name.Length == 0)
                throw new InvalidDataException($"Packaged catalogue {catalogue.Asset} contains an unnamed entry.");
            var row = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["name_lower"] = name.ToLowerInvariant(),
                ["description"] = Text(entry, "description"),
                // Store the complete original object, including fields unknown to
                // the denormalised search schema (Heal, immunities, sourcePage...).
                ["data_json"] = entry.GetRawText(),
                [catalogue.PowerColumn] = Number(entry, catalogue.PowerColumn)
            };
            foreach (var definition in catalogue.Columns)
            {
                var column = definition.Split(' ')[0];
                if (row.ContainsKey(column)) continue;
                row[column] = column switch
                {
                    "is_default" => 1,
                    "is_advanced" => Boolean(entry, "isAdvanced") ? 1 : 0,
                    "fields_json" => Property(entry, "fields") is { } fields ? fields.GetRawText() : "[]",
                    "damage_json" => Property(entry, "Damage") is { } damage ? damage.GetRawText() : null,
                    "as_per" => Text(entry, "asPer"),
                    "type" => NormalizeNeuronicType(Text(entry, "tree")),
                    _ => Text(entry, column)
                };
            }
            rows.Add(row);
        }
        if (rows.Count == 0)
            throw new InvalidDataException($"Packaged catalogue {catalogue.Asset} is empty.");
        return rows;
    }

    private static void EnsureTable(SqliteConnection connection, SqliteTransaction transaction, Catalogue catalogue)
    {
        var columns = string.Join(",", catalogue.Columns);
        Execute(connection, transaction, $"""
            CREATE TABLE IF NOT EXISTS {catalogue.Table} (
                id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL, name_lower TEXT,
                description TEXT, data_json TEXT, created_at TEXT, updated_at TEXT, {columns});
            CREATE INDEX IF NOT EXISTS idx_{catalogue.Table}_name_lower ON {catalogue.Table}(name_lower);
            """);
    }

    private static HashSet<string> ReadExistingKeys(SqliteConnection connection, SqliteTransaction transaction, Catalogue catalogue)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var columns = new[] { "name", catalogue.PowerColumn, "data_json" }.Concat(catalogue.Dimensions)
            .Concat(catalogue.Table == "neuronics" ? [] : new[] { "is_advanced" }).Distinct().ToList();
        command.CommandText = $"SELECT {string.Join(',', columns)} FROM {catalogue.Table};";
        using var reader = command.ExecuteReader();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            var row = columns.Select((column, index) => (column, value: reader.IsDBNull(index) ? null : reader.GetValue(index)))
                .ToDictionary(pair => pair.column, pair => pair.value, StringComparer.Ordinal);
            // A nonstandard row with the same name must remain independent of
            // the packaged default. Never overwrite or delete either row.
            if (IsNonStandard(row["data_json"]?.ToString())) continue;
            keys.Add(Identity(catalogue, row));
        }
        return keys;
    }

    private static int InsertRow(SqliteConnection connection, SqliteTransaction transaction, Catalogue catalogue,
        Dictionary<string, object?> row, string identity)
    {
        var now = DateTime.UtcNow.ToString("O");
        row["id"] = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"catalogue|{catalogue.Table}|{identity}"))[..16]).ToString();
        row["created_at"] = now;
        row["updated_at"] = now;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT OR IGNORE INTO {catalogue.Table} ({string.Join(',', row.Keys)}) VALUES ({string.Join(',', row.Keys.Select(key => "$" + key))});";
        foreach (var (key, value) in row)
            command.Parameters.AddWithValue("$" + key, value ?? DBNull.Value);
        return command.ExecuteNonQuery();
    }

    private static string Identity(Catalogue catalogue, IReadOnlyDictionary<string, object?> row)
    {
        var values = new[] { "name", catalogue.PowerColumn }.Concat(catalogue.Dimensions)
            .Concat(catalogue.Table == "neuronics" ? [] : new[] { "is_advanced" });
        return JsonSerializer.Serialize(values.Select(column =>
        {
            var value = row.GetValueOrDefault(column)?.ToString()?.Trim() ?? string.Empty;
            if (column == "is_advanced" && value.Length == 0) value = "0";
            if (column == "type") value = NormalizeNeuronicType(value);
            if (column == "fields_json")
            {
                // Whitespace/order differences in an existing JSON array do not
                // turn the same evocation into a second search result.
                using var fields = JsonDocument.Parse(value.Length == 0 ? "[]" : value);
                value = JsonSerializer.Serialize(fields.RootElement.EnumerateArray().Select(field => field.GetString()?.Trim().ToLowerInvariant()).Order());
            }
            return value.ToLowerInvariant();
        }));
    }

    private static bool IsNonStandard(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return Boolean(document.RootElement, "NonStandard");
        }
        catch (JsonException) { return false; }
    }

    private static JsonElement? Property(JsonElement entry, string name)
    {
        if (entry.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in entry.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return null;
    }
    private static string Text(JsonElement entry, string name) => Property(entry, name) is { } value
        ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
        : string.Empty;
    private static int Number(JsonElement entry, string name) => int.TryParse(Text(entry, name), out var number) ? number : 0;
    private static bool Boolean(JsonElement entry, string name) => Property(entry, name) is { } value
        && (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var flag) && flag));
    private static string NormalizeNeuronicType(string type) => type.Trim().ToLowerInvariant() switch
        { "active" => "Active", "passive" => "Passive", _ => "None" };

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void RebuildEvocationSearchIndexes(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var hasFullTextSearch = true;
        try
        {
            Execute(connection, transaction, """
                CREATE VIRTUAL TABLE IF NOT EXISTS evocs_fts USING fts5(name, description);
                CREATE TABLE IF NOT EXISTS evocs_fts_map(fts_rowid INTEGER PRIMARY KEY, evoc_id TEXT);
                DELETE FROM evocs_fts;
                DELETE FROM evocs_fts_map;
                """);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1
            && ex.Message.Contains("no such module: fts5", StringComparison.OrdinalIgnoreCase))
        {
            // FTS5 is optional on some mobile SQLite builds. Catalogue browsing
            // and ngram search remain available without the virtual table.
            hasFullTextSearch = false;
        }
        Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS evoc_ngrams(token TEXT NOT NULL, evoc_id TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_evoc_ngrams_token ON evoc_ngrams(token);
            DELETE FROM evoc_ngrams;
            """);
        var rows = new List<(string Id, string Name, string Description)>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT id, name, COALESCE(description,'') FROM evocs;";
            using var reader = command.ExecuteReader();
            while (reader.Read()) rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (hasFullTextSearch)
            {
                command.CommandText = """
                    INSERT INTO evocs_fts(name, description) VALUES ($name,$description);
                    INSERT INTO evocs_fts_map(fts_rowid,evoc_id) VALUES (last_insert_rowid(),$id);
                    """;
                command.Parameters.AddWithValue("$id", row.Id);
                command.Parameters.AddWithValue("$name", row.Name);
                command.Parameters.AddWithValue("$description", row.Description);
                command.ExecuteNonQuery();
            }
            var normalized = ServiceHelper.NormalizeForNgrams(row.Name.ToLowerInvariant());
            var tokens = ServiceHelper.GenerateNGrams(normalized, 3).Distinct(StringComparer.Ordinal);
            foreach (var token in tokens)
            {
                command.CommandText = "INSERT INTO evoc_ngrams(token, evoc_id) VALUES ($token,$id);";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", row.Id);
                command.Parameters.AddWithValue("$token", token);
                command.ExecuteNonQuery();
            }
        }
    }

    private sealed record Catalogue(string Table, string Asset, string PowerColumn, string[] Dimensions, string[] Columns);
}
