using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public interface IEvolutionDataSynchronizer
{
    Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default);
}

public sealed class EvolutionDataSynchronizer : IEvolutionDataSynchronizer
{
    private const string SeedVersion = "evolution-defaults-v1";
    private const int NgramSize = 3;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<EvolutionDataSynchronizer> _logger;

    public EvolutionDataSynchronizer(ILogger<EvolutionDataSynchronizer> logger)
    {
        _logger = logger;
    }

    public async Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            return;

        var mergedJson = await ReadAssetTextAsync("evolution_classes/merged.json", cancellationToken);
        if (string.IsNullOrWhiteSpace(mergedJson))
        {
            _logger.LogWarning("Skipped evolution sync: could not load packaged evolution_classes/merged.json.");
            return;
        }

        var makesJson = await ReadAssetTextAsync("makes_abilities.json", cancellationToken);
        var checksum = ComputeChecksum($"{mergedJson}\n{makesJson}");

        var defaults = LoadDefaults(mergedJson, makesJson);
        if (defaults.Count == 0)
        {
            _logger.LogWarning("Skipped evolution sync: no default abilities were parsed.");
            return;
        }

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(cancellationToken);
        EnsureTablesExist(conn);

        var existingChecksum = GetExistingChecksum(conn);
        if (string.Equals(existingChecksum, checksum, StringComparison.OrdinalIgnoreCase))
            return;

        using var tx = conn.BeginTransaction();
        var nonStandardIds = LoadNonStandardIds(conn, tx);
        var defaultIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ability in defaults.Values)
        {
            defaultIds.Add(ability.Id);
            if (nonStandardIds.Contains(ability.Id))
                continue;

            UpsertDefaultAbility(conn, tx, ability);
        }

        DeleteObsoleteDefaults(conn, tx, defaultIds);
        RebuildEvolutionNgrams(conn, tx);
        SaveChecksum(conn, tx, checksum);
        tx.Commit();

        EvolutionService.InvalidateCache();
        _logger.LogInformation("Evolution defaults synchronized. Upserted {Count} defaults.", defaults.Count);
    }

    private static Dictionary<string, EvolutionDefaultAbility> LoadDefaults(string mergedJson, string makesJson)
    {
        var map = new Dictionary<string, EvolutionDefaultAbility>(StringComparer.OrdinalIgnoreCase);

        foreach (var ability in ParseAbilitySeedList(mergedJson, sourceBookFallback: string.Empty))
            map[BuildKey(ability.Table, ability.Index)] = ability;

        foreach (var ability in ParseAbilitySeedList(makesJson, sourceBookFallback: "Classes"))
            map[BuildKey(ability.Table, ability.Index)] = ability;

        return map;
    }

    private static IEnumerable<EvolutionDefaultAbility> ParseAbilitySeedList(string rawJson, string sourceBookFallback)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return Enumerable.Empty<EvolutionDefaultAbility>();

        List<AbilitySeed>? seeds;
        try
        {
            seeds = JsonSerializer.Deserialize<List<AbilitySeed>>(rawJson, JsonOptions);
        }
        catch
        {
            return Enumerable.Empty<EvolutionDefaultAbility>();
        }

        if (seeds is null || seeds.Count == 0)
            return Enumerable.Empty<EvolutionDefaultAbility>();

        var list = new List<EvolutionDefaultAbility>(seeds.Count);
        foreach (var seed in seeds)
        {
            var index = (seed.index ?? string.Empty).Trim();
            if (index.Length == 0)
                continue;

            var description = (seed.desc ?? string.Empty).Trim();
            var table = Math.Max(0, seed.table);
            var availableRaw = SerializeAvailable(seed.available);
            var parsedCost = ParseCost(seed.cost, seed.canBuyMultiple ?? false);
            var preReqs = NormalizePreReqs(seed.preReqs);
            var sourceBook = (seed.sourceBook ?? string.Empty).Trim();
            if (sourceBook.Length == 0)
                sourceBook = sourceBookFallback;
            var abilityRef = (seed.abilityRef ?? string.Empty).Trim();

            var id = DeterministicGuid($"evo|{table}|{index}");
            var dataJson = JsonSerializer.Serialize(new
            {
                available = availableRaw,
                index,
                desc = description,
                cost = parsedCost.Cost,
                table,
                canBuyMultiple = parsedCost.CanBuyMultiple,
                preReqs,
                sourceBook,
                abilityRef
            });

            list.Add(new EvolutionDefaultAbility(
                Id: id,
                Index: index,
                Description: description,
                Cost: parsedCost.Cost,
                Table: table,
                AvailableRaw: availableRaw,
                CanBuyMultiple: parsedCost.CanBuyMultiple,
                PreReqsJson: preReqs is { Count: > 0 } ? JsonSerializer.Serialize(preReqs) : null,
                DataJson: dataJson));
        }

        return list;
    }

    private static string SerializeAvailable(JsonElement available)
    {
        if (available.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return "[]";

        return available.GetRawText();
    }

    private static List<string> NormalizePreReqs(List<string>? preReqs)
        => (preReqs ?? new List<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static (int Cost, bool CanBuyMultiple) ParseCost(JsonElement costElement, bool explicitCanBuyMultiple)
    {
        var raw = costElement.ValueKind switch
        {
            JsonValueKind.Number when costElement.TryGetInt32(out var asNumber) => asNumber.ToString(),
            JsonValueKind.String => (costElement.GetString() ?? string.Empty).Trim(),
            _ => string.Empty
        };

        var cost = ParseFirstInteger(raw);
        var canBuyMultiple = explicitCanBuyMultiple || raw.Contains('*');
        return (cost, canBuyMultiple);
    }

    private static int ParseFirstInteger(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var digits = new StringBuilder();
        var seenDigit = false;
        foreach (var ch in raw)
        {
            if (char.IsDigit(ch))
            {
                seenDigit = true;
                digits.Append(ch);
                continue;
            }

            if (seenDigit)
                break;
        }

        return int.TryParse(digits.ToString(), out var parsed) ? parsed : 0;
    }

    private static HashSet<string> LoadNonStandardIds(SqliteConnection conn, SqliteTransaction tx)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM evolution WHERE is_default = 0;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(0))
                continue;

            var id = (reader.GetString(0) ?? string.Empty).Trim();
            if (id.Length > 0)
                ids.Add(id);
        }

        return ids;
    }

    private static void UpsertDefaultAbility(SqliteConnection conn, SqliteTransaction tx, EvolutionDefaultAbility ability)
    {
        var now = DateTime.UtcNow.ToString("o");
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO evolution
(id, idx, idx_lower, description, cost, available, table_id, can_buy_multiple, prereqs_json, data_json, is_default, created_at, updated_at)
VALUES ($id, $idx, $idxLower, $description, $cost, $available, $table, $canBuyMultiple, $preReqs, $data, 1, $createdAt, $updatedAt)
ON CONFLICT(id) DO UPDATE SET
    idx = excluded.idx,
    idx_lower = excluded.idx_lower,
    description = excluded.description,
    cost = excluded.cost,
    available = excluded.available,
    table_id = excluded.table_id,
    can_buy_multiple = excluded.can_buy_multiple,
    prereqs_json = excluded.prereqs_json,
    data_json = excluded.data_json,
    is_default = 1,
    updated_at = excluded.updated_at;
";
        cmd.Parameters.AddWithValue("$id", ability.Id);
        cmd.Parameters.AddWithValue("$idx", ability.Index);
        cmd.Parameters.AddWithValue("$idxLower", ability.Index.ToLowerInvariant());
        cmd.Parameters.AddWithValue("$description", ability.Description);
        cmd.Parameters.AddWithValue("$cost", ability.Cost);
        cmd.Parameters.AddWithValue("$available", ability.AvailableRaw);
        cmd.Parameters.AddWithValue("$table", ability.Table);
        cmd.Parameters.AddWithValue("$canBuyMultiple", ability.CanBuyMultiple ? 1 : 0);
        cmd.Parameters.AddWithValue("$preReqs", (object?)ability.PreReqsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$data", ability.DataJson);
        cmd.Parameters.AddWithValue("$createdAt", now);
        cmd.Parameters.AddWithValue("$updatedAt", now);
        cmd.ExecuteNonQuery();
    }

    private static void DeleteObsoleteDefaults(
        SqliteConnection conn,
        SqliteTransaction tx,
        IReadOnlyCollection<string> currentDefaultIds)
    {
        if (currentDefaultIds.Count == 0)
        {
            using var deleteAll = conn.CreateCommand();
            deleteAll.Transaction = tx;
            deleteAll.CommandText = "DELETE FROM evolution WHERE is_default = 1;";
            deleteAll.ExecuteNonQuery();
            return;
        }

        using var createTemp = conn.CreateCommand();
        createTemp.Transaction = tx;
        createTemp.CommandText = "CREATE TEMP TABLE IF NOT EXISTS tmp_current_defaults (id TEXT PRIMARY KEY);";
        createTemp.ExecuteNonQuery();

        using var clearTemp = conn.CreateCommand();
        clearTemp.Transaction = tx;
        clearTemp.CommandText = "DELETE FROM tmp_current_defaults;";
        clearTemp.ExecuteNonQuery();

        using (var insertTemp = conn.CreateCommand())
        {
            insertTemp.Transaction = tx;
            insertTemp.CommandText = "INSERT OR IGNORE INTO tmp_current_defaults (id) VALUES ($id);";
            var idParam = insertTemp.Parameters.Add("$id", SqliteType.Text);
            foreach (var id in currentDefaultIds)
            {
                idParam.Value = id;
                insertTemp.ExecuteNonQuery();
            }
        }

        using var deleteMissing = conn.CreateCommand();
        deleteMissing.Transaction = tx;
        deleteMissing.CommandText = @"
DELETE FROM evolution
WHERE is_default = 1
  AND id NOT IN (SELECT id FROM tmp_current_defaults);";
        deleteMissing.ExecuteNonQuery();

        using var dropTemp = conn.CreateCommand();
        dropTemp.Transaction = tx;
        dropTemp.CommandText = "DROP TABLE IF EXISTS tmp_current_defaults;";
        dropTemp.ExecuteNonQuery();
    }

    private static void RebuildEvolutionNgrams(SqliteConnection conn, SqliteTransaction tx)
    {
        using (var clear = conn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM evolution_ngrams;";
            clear.ExecuteNonQuery();
        }

        var rows = new List<(string Id, string Index, string Description)>();
        using (var read = conn.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = "SELECT id, idx, description FROM evolution;";
            using var reader = read.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(0))
                    continue;

                var id = (reader.GetString(0) ?? string.Empty).Trim();
                if (id.Length == 0)
                    continue;

                var idx = reader.IsDBNull(1) ? string.Empty : (reader.GetString(1) ?? string.Empty);
                var description = reader.IsDBNull(2) ? string.Empty : (reader.GetString(2) ?? string.Empty);
                rows.Add((id, idx, description));
            }
        }

        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO evolution_ngrams (token, evolution_id) VALUES ($token, $evolutionId);";
        var tokenParam = insert.Parameters.Add("$token", SqliteType.Text);
        var evolutionIdParam = insert.Parameters.Add("$evolutionId", SqliteType.Text);

        foreach (var row in rows)
        {
            var combined = $"{row.Index} {row.Description}".ToLowerInvariant();
            var normalized = ServiceHelper.NormalizeForNgrams(combined);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in ServiceHelper.GenerateNGrams(normalized, NgramSize))
            {
                var trimmed = (token ?? string.Empty).Trim();
                if (trimmed.Length == 0 || !unique.Add(trimmed))
                    continue;

                tokenParam.Value = trimmed;
                evolutionIdParam.Value = row.Id;
                insert.ExecuteNonQuery();
            }
        }
    }

    private static void SaveChecksum(SqliteConnection conn, SqliteTransaction tx, string checksum)
    {
        using (var delete = conn.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM seed_metadata WHERE seed_version = $seedVersion;";
            delete.Parameters.AddWithValue("$seedVersion", SeedVersion);
            delete.ExecuteNonQuery();
        }

        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = @"
INSERT INTO seed_metadata (seed_version, schema_version, build_id, checksum, created_at)
VALUES ($seedVersion, $schemaVersion, $buildId, $checksum, $createdAt);";
        insert.Parameters.AddWithValue("$seedVersion", SeedVersion);
        insert.Parameters.AddWithValue("$schemaVersion", 1);
        insert.Parameters.AddWithValue("$buildId", "evolution-sync");
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

    private static void EnsureTablesExist(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS evolution (
  id TEXT PRIMARY KEY,
  idx TEXT NOT NULL,
  idx_lower TEXT,
  description TEXT,
  cost INTEGER,
  available TEXT,
  table_id INTEGER,
  can_buy_multiple INTEGER,
  prereqs_json TEXT,
  data_json TEXT,
  is_default INTEGER,
  created_at TEXT,
  updated_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_evolution_idx_lower ON evolution(idx_lower);

CREATE TABLE IF NOT EXISTS evolution_ngrams (
  token TEXT,
  evolution_id TEXT
);
CREATE INDEX IF NOT EXISTS idx_evolution_ngrams_token ON evolution_ngrams(token);
CREATE INDEX IF NOT EXISTS idx_evolution_ngrams_evolution_id ON evolution_ngrams(evolution_id);

CREATE TABLE IF NOT EXISTS seed_metadata (
  seed_version TEXT,
  schema_version INTEGER,
  build_id TEXT,
  checksum TEXT,
  created_at TEXT
);";
        cmd.ExecuteNonQuery();
    }

    private static async Task<string> ReadAssetTextAsync(string logicalName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
            return string.Empty;

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(logicalName);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ComputeChecksum(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string BuildKey(int table, string index)
        => $"{Math.Max(0, table)}|{(index ?? string.Empty).Trim().ToLowerInvariant()}";

    private static string DeterministicGuid(string input)
    {
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes((input ?? string.Empty).ToLowerInvariant()));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes).ToString();
    }

    private sealed class AbilitySeed
    {
        public JsonElement available { get; set; }
        public string index { get; set; } = string.Empty;
        public string? desc { get; set; }
        public JsonElement cost { get; set; }
        public int table { get; set; }
        public List<string>? preReqs { get; set; }
        public bool? canBuyMultiple { get; set; }
        public string? sourceBook { get; set; }
        public string? abilityRef { get; set; }
    }

    private sealed record EvolutionDefaultAbility(
        string Id,
        string Index,
        string Description,
        int Cost,
        int Table,
        string AvailableRaw,
        bool CanBuyMultiple,
        string? PreReqsJson,
        string DataJson);
}
