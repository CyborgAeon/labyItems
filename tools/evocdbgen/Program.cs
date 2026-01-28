using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

internal class EvocRaw
{
    public string name { get; set; } = string.Empty;
    public int power { get; set; }
    public string range { get; set; } = string.Empty;
    public string duration { get; set; } = string.Empty;
    public string verbal { get; set; } = string.Empty;
    public List<string>? fields { get; set; }
    public string description { get; set; } = string.Empty;
    public bool isAdvanced { get; set; }
}

internal sealed class TableRaw
{
    public string? Available { get; set; }
    public string Index { get; set; } = string.Empty;
    public string? Desc { get; set; }
    public string? Cost { get; set; }
}

internal sealed class AbilityRaw
{
    public string? Available { get; set; }
    public string Index { get; set; } = string.Empty;
    public string? Desc { get; set; }
    public string? Cost { get; set; }
    public int Table { get; set; }
    public List<string>? PreReqs { get; set; }
}

class Program
{
    static int Main(string[] args)
    {
        var input = args.Length > 0 ? args[0] : "../labyItems/Resources/Raw/druids_way/evocs.json";
        var output = args.Length > 1 ? args[1] : "output/default.db";

        if (!File.Exists(input))
        {
            return 2;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");

        var json = File.ReadAllText(input);
        var list = JsonSerializer.Deserialize<List<EvocRaw>>(json) ?? new List<EvocRaw>();

        if (File.Exists(output))
            File.Delete(output);

        using var conn = new SqliteConnection($"Data Source={output}");
        conn.Open();

        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE evocs (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  name_lower TEXT,
  power INTEGER,
  range TEXT,
  duration TEXT,
  verbal TEXT,
  fields_json TEXT,
  description TEXT,
  is_advanced INTEGER,
  data_json TEXT,
  is_default INTEGER,
  created_at TEXT,
  updated_at TEXT
);
CREATE INDEX idx_evocs_name_lower ON evocs(name_lower);

-- FTS5 table for name/description (for phrase/token searches)
CREATE VIRTUAL TABLE evocs_fts USING fts5(name, description);
CREATE TABLE evocs_fts_map(fts_rowid INTEGER PRIMARY KEY, evoc_id TEXT);
CREATE INDEX idx_evocs_fts_map_evoc_id ON evocs_fts_map(evoc_id);

-- n-gram tokens to support substring searches
CREATE TABLE evoc_ngrams(token TEXT, evoc_id TEXT);
CREATE INDEX idx_evoc_ngrams_token ON evoc_ngrams(token);
CREATE INDEX idx_evoc_ngrams_evoc_id ON evoc_ngrams(evoc_id);

-- Evolution classes table (combined from evolution_classes/table_1..table_N)
CREATE TABLE evolution (
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
CREATE INDEX idx_evolution_idx_lower ON evolution(idx_lower);
CREATE TABLE evolution_ngrams(token TEXT, evolution_id TEXT);
CREATE INDEX idx_evolution_ngrams_token ON evolution_ngrams(token);
CREATE INDEX idx_evolution_ngrams_evolution_id ON evolution_ngrams(evolution_id);

-- Make abilities table
CREATE TABLE abilities (
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
CREATE INDEX idx_abilities_idx_lower ON abilities(idx_lower);
CREATE TABLE abilities_ngrams(token TEXT, ability_id TEXT);
CREATE INDEX idx_abilities_ngrams_token ON abilities_ngrams(token);
CREATE INDEX idx_abilities_ngrams_ability_id ON abilities_ngrams(ability_id);

-- Seed metadata for CI and runtime to validate seed provenance
CREATE TABLE seed_metadata (
  seed_version TEXT,
  schema_version INTEGER,
  build_id TEXT,
  checksum TEXT,
  created_at TEXT
);
";
            cmd.ExecuteNonQuery();
        }

        var insertEvocCmd = conn.CreateCommand();
        insertEvocCmd.CommandText = @"
INSERT INTO evocs (id, name, name_lower, power, range, duration, verbal, fields_json, description, is_advanced, data_json, is_default, created_at, updated_at)
VALUES (@id, @name, @name_lower, @power, @range, @duration, @verbal, @fields_json, @description, @is_advanced, @data_json, @is_default, @created_at, @updated_at);
";

        var insertFtsCmd = conn.CreateCommand();
        insertFtsCmd.CommandText = @"INSERT INTO evocs_fts (name, description) VALUES (@name, @description);";

        var insertFtsMapCmd = conn.CreateCommand();
        insertFtsMapCmd.CommandText = @"INSERT INTO evocs_fts_map (fts_rowid, evoc_id) VALUES (@fts_rowid, @evoc_id);";

        var insertNgramCmd = conn.CreateCommand();
        insertNgramCmd.CommandText = @"INSERT INTO evoc_ngrams (token, evoc_id) VALUES (@token, @evoc_id);";

        foreach (var e in list)
        {
            var id = DeterministicGuid(e.name + "|" + e.description).ToString();
            insertEvocCmd.Parameters.Clear();
            insertEvocCmd.Parameters.AddWithValue("@id", id);
            insertEvocCmd.Parameters.AddWithValue("@name", e.name);
            insertEvocCmd.Parameters.AddWithValue("@name_lower", e.name.ToLowerInvariant());
            insertEvocCmd.Parameters.AddWithValue("@power", e.power);
            insertEvocCmd.Parameters.AddWithValue("@range", e.range ?? string.Empty);
            insertEvocCmd.Parameters.AddWithValue("@duration", e.duration ?? string.Empty);
            insertEvocCmd.Parameters.AddWithValue("@verbal", e.verbal ?? string.Empty);
            var fieldsJson = e.fields is null ? null : JsonSerializer.Serialize(e.fields);
            insertEvocCmd.Parameters.AddWithValue("@fields_json", (object?)fieldsJson ?? DBNull.Value);
            insertEvocCmd.Parameters.AddWithValue("@description", e.description ?? string.Empty);
            insertEvocCmd.Parameters.AddWithValue("@is_advanced", e.isAdvanced ? 1 : 0);
            insertEvocCmd.Parameters.AddWithValue("@data_json", (object?)JsonSerializer.Serialize(e) ?? DBNull.Value);
            insertEvocCmd.Parameters.AddWithValue("@is_default", 1);
            var now = DateTime.UtcNow.ToString("o");
            insertEvocCmd.Parameters.AddWithValue("@created_at", now);
            insertEvocCmd.Parameters.AddWithValue("@updated_at", now);

            insertEvocCmd.ExecuteNonQuery();

            // Populate FTS table and map
            insertFtsCmd.Parameters.Clear();
            insertFtsCmd.Parameters.AddWithValue("@name", e.name);
            insertFtsCmd.Parameters.AddWithValue("@description", e.description ?? string.Empty);
            insertFtsCmd.ExecuteNonQuery();

            // retrieve the last inserted rowid from SQLite
            using (var lastCmd = conn.CreateCommand())
            {
                lastCmd.CommandText = "SELECT last_insert_rowid();";
                long ftsRowId = (long)(lastCmd.ExecuteScalar() ?? 0L);
                insertFtsMapCmd.Parameters.Clear();
                insertFtsMapCmd.Parameters.AddWithValue("@fts_rowid", ftsRowId);
                insertFtsMapCmd.Parameters.AddWithValue("@evoc_id", id);
                insertFtsMapCmd.ExecuteNonQuery();
            }

            // Build n-grams from name + description
            var combined = (e.name + " " + (e.description ?? string.Empty)).ToLowerInvariant();
            var normalized = NormalizeForNgrams(combined);
            var tokens = GenerateNGrams(normalized, 3);
            var inserted = new HashSet<string>();
            foreach (var t in tokens)
            {
                if (inserted.Add(t))
                {
                    insertNgramCmd.Parameters.Clear();
                    insertNgramCmd.Parameters.AddWithValue("@token", t);
                    insertNgramCmd.Parameters.AddWithValue("@evoc_id", id);
                    insertNgramCmd.ExecuteNonQuery();
                }
            }
        }

        // Now import evolution_classes tables into 'evolution' table
        // derive resources root from the input path (input usually points at druids_way/evocs.json)
        var inputDir = Path.GetDirectoryName(input) ?? ".";
        var resourcesRoot = Path.GetFullPath(Path.Combine(inputDir, "..")); // Resources/Raw

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var normalizedJsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        for (int tableNum = 1; tableNum <= 12; tableNum++)
        {
            var path = Path.GetFullPath(Path.Combine(resourcesRoot, "evolution_classes", $"table_{tableNum}.json"));
            if (!File.Exists(path))
            {
                continue;
            }

            var jsonTable = File.ReadAllText(path);
            var rawTable = JsonSerializer.Deserialize<List<TableRaw>>(jsonTable, jsonOptions) ?? new List<TableRaw>();

            var insertEvoCmd = conn.CreateCommand();
            insertEvoCmd.CommandText = @"INSERT INTO evolution (id, idx, idx_lower, description, cost, available, table_id, can_buy_multiple, prereqs_json, data_json, is_default, created_at, updated_at) VALUES (@id, @idx, @idx_lower, @description, @cost, @available, @table_id, @can_buy_multiple, @prereqs_json, @data_json, @is_default, @created_at, @updated_at);";
            var insertEvoNgramCmd = conn.CreateCommand();
            insertEvoNgramCmd.CommandText = @"INSERT INTO evolution_ngrams (token, evolution_id) VALUES (@token, @evolution_id);";

            var insertedCount = 0;
            foreach (var r in rawTable)
            {
                var idx = (r.Index ?? "").Trim();
                if (string.IsNullOrWhiteSpace(idx)) continue;
                var desc = (r.Desc ?? "").Trim();
                var available = (r.Available ?? "").Trim();
                var costRaw = (r.Cost ?? "").Trim();
                var canBuyMultiple = costRaw.Contains('*');
                var hasPlus = costRaw.Contains('+');
                var cost = TryParseCostForTool(costRaw);
                List<string>? preReqs = hasPlus ? new List<string>() : null;
                var id = DeterministicGuid($"evo|{tableNum}|{idx}").ToString();

                insertEvoCmd.Parameters.Clear();
                insertEvoCmd.Parameters.AddWithValue("@id", id);
                insertEvoCmd.Parameters.AddWithValue("@idx", idx);
                insertEvoCmd.Parameters.AddWithValue("@idx_lower", idx.ToLowerInvariant());
                insertEvoCmd.Parameters.AddWithValue("@description", desc);
                insertEvoCmd.Parameters.AddWithValue("@cost", cost);
                insertEvoCmd.Parameters.AddWithValue("@available", available);
                insertEvoCmd.Parameters.AddWithValue("@table_id", tableNum);
                insertEvoCmd.Parameters.AddWithValue("@can_buy_multiple", canBuyMultiple ? 1 : 0);
                insertEvoCmd.Parameters.AddWithValue("@prereqs_json", preReqs is null ? DBNull.Value : JsonSerializer.Serialize(preReqs));
                var evoJson = new
                {
                    available,
                    index = idx,
                    desc,
                    cost,
                    table = tableNum,
                    canBuyMultiple,
                    preReqs
                };
                insertEvoCmd.Parameters.AddWithValue("@data_json", JsonSerializer.Serialize(evoJson, normalizedJsonOptions));
                insertEvoCmd.Parameters.AddWithValue("@is_default", 1);
                var now2 = DateTime.UtcNow.ToString("o");
                insertEvoCmd.Parameters.AddWithValue("@created_at", now2);
                insertEvoCmd.Parameters.AddWithValue("@updated_at", now2);
                insertEvoCmd.ExecuteNonQuery();
                insertedCount++;

                var combined = (idx + " " + desc).ToLowerInvariant();
                var normalized = NormalizeForNgrams(combined);
                var tokens = GenerateNGrams(normalized, 3);
                var inserted = new HashSet<string>();
                foreach (var tkn in tokens)
                {
                    if (inserted.Add(tkn))
                    {
                        insertEvoNgramCmd.Parameters.Clear();
                        insertEvoNgramCmd.Parameters.AddWithValue("@token", tkn);
                        insertEvoNgramCmd.Parameters.AddWithValue("@evolution_id", id);
                        insertEvoNgramCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // Import make abilities into abilities table (if present)
        var abilitiesPath = Path.GetFullPath(Path.Combine(resourcesRoot, "makes_abilities.json"));
        if (File.Exists(abilitiesPath))
        {
            var rawAbilities = JsonSerializer.Deserialize<List<AbilityRaw>>(File.ReadAllText(abilitiesPath), jsonOptions)
                               ?? new List<AbilityRaw>();

            var insertAbilityCmd = conn.CreateCommand();
            insertAbilityCmd.CommandText = @"INSERT INTO abilities (id, idx, idx_lower, description, cost, available, table_id, can_buy_multiple, prereqs_json, data_json, is_default, created_at, updated_at) VALUES (@id, @idx, @idx_lower, @description, @cost, @available, @table_id, @can_buy_multiple, @prereqs_json, @data_json, @is_default, @created_at, @updated_at);";

            var insertAbilityNgramCmd = conn.CreateCommand();
            insertAbilityNgramCmd.CommandText = @"INSERT INTO abilities_ngrams (token, ability_id) VALUES (@token, @ability_id);";

            foreach (var r in rawAbilities)
            {
                var idx = (r.Index ?? "").Trim();
                if (string.IsNullOrWhiteSpace(idx)) continue;

                var desc = (r.Desc ?? "").Trim();
                var available = (r.Available ?? "").Trim();
                var costRaw = (r.Cost ?? "").Trim();
                var canBuyMultiple = costRaw.Contains('*');
                var hasPlus = costRaw.Contains('+');
                var cost = TryParseCostForTool(costRaw);
                var table = r.Table;

                List<string>? preReqs = null;
                if (hasPlus)
                    preReqs = (r.PreReqs is { Count: > 0 } ? r.PreReqs : new List<string>());
                else if (r.PreReqs is { Count: > 0 })
                    preReqs = r.PreReqs;

                var id = DeterministicGuid($"ability|{table}|{idx}").ToString();

                insertAbilityCmd.Parameters.Clear();
                insertAbilityCmd.Parameters.AddWithValue("@id", id);
                insertAbilityCmd.Parameters.AddWithValue("@idx", idx);
                insertAbilityCmd.Parameters.AddWithValue("@idx_lower", idx.ToLowerInvariant());
                insertAbilityCmd.Parameters.AddWithValue("@description", desc);
                insertAbilityCmd.Parameters.AddWithValue("@cost", cost);
                insertAbilityCmd.Parameters.AddWithValue("@available", available);
                insertAbilityCmd.Parameters.AddWithValue("@table_id", table);
                insertAbilityCmd.Parameters.AddWithValue("@can_buy_multiple", canBuyMultiple ? 1 : 0);
                insertAbilityCmd.Parameters.AddWithValue("@prereqs_json", preReqs is null ? DBNull.Value : JsonSerializer.Serialize(preReqs));
                var abilityJson = new
                {
                    available,
                    index = idx,
                    desc,
                    cost,
                    table,
                    canBuyMultiple,
                    preReqs
                };
                insertAbilityCmd.Parameters.AddWithValue("@data_json", JsonSerializer.Serialize(abilityJson, normalizedJsonOptions));
                insertAbilityCmd.Parameters.AddWithValue("@is_default", 1);
                var now3 = DateTime.UtcNow.ToString("o");
                insertAbilityCmd.Parameters.AddWithValue("@created_at", now3);
                insertAbilityCmd.Parameters.AddWithValue("@updated_at", now3);
                insertAbilityCmd.ExecuteNonQuery();

                var combined = (idx + " " + desc).ToLowerInvariant();
                var normalized = NormalizeForNgrams(combined);
                var tokens = GenerateNGrams(normalized, 3);
                var inserted = new HashSet<string>();
                foreach (var tkn in tokens)
                {
                    if (inserted.Add(tkn))
                    {
                        insertAbilityNgramCmd.Parameters.Clear();
                        insertAbilityNgramCmd.Parameters.AddWithValue("@token", tkn);
                        insertAbilityNgramCmd.Parameters.AddWithValue("@ability_id", id);
                        insertAbilityNgramCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        tx.Commit();
        conn.Close();

        // Insert seed metadata (seed_version & build id). Checksum handling is intentionally skipped.
        var buildId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? Environment.GetEnvironmentVariable("SEED_BUILD_ID") ?? Guid.NewGuid().ToString();
        var seedVersion = Environment.GetEnvironmentVariable("SEED_VERSION") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var schemaVersion = 1; // base schema applied by this generator; migrations may bump this later

        using (var conn2 = new SqliteConnection($"Data Source={output}"))
        {
            conn2.Open();
            using (var cmd = conn2.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO seed_metadata (seed_version, schema_version, build_id, created_at) VALUES (@seed_version, @schema_version, @build_id, @created_at);";
                cmd.Parameters.AddWithValue("@seed_version", seedVersion);
                cmd.Parameters.AddWithValue("@schema_version", schemaVersion);
                cmd.Parameters.AddWithValue("@build_id", buildId);
                cmd.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            conn2.Close();
        }

        return 0;
    }

    static Guid DeterministicGuid(string input)
    {
        // Create a deterministic GUID from the SHA1 hash of the input
        using var sha1 = SHA1.Create();
        var data = Encoding.UTF8.GetBytes(input.ToLowerInvariant());
        var hash = sha1.ComputeHash(data);
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    static string NormalizeForNgrams(string s)
    {
        // normalize whitespace and remove non-printable characters
        var b = new StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)) b.Append(ch);
            else b.Append(' ');
        }
        var normalized = string.Join(' ', b.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        return normalized;
    }

    static IEnumerable<string> GenerateNGrams(string s, int n)
    {
        // Generate overlapping n-grams for the provided string (including across words)
        var compact = s.Replace(" ", " "); // already normalized
        var t = compact;
        if (t.Length <= n)
        {
            yield return t;
            yield break;
        }

        for (int i = 0; i <= t.Length - n; i++)
        {
            yield return t.Substring(i, n);
        }
    }

    private static int TryParseCostForTool(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0;
        var digitsOnly = System.Text.RegularExpressions.Regex.Replace(input, "[^0-9]", "");
        return int.TryParse(digitsOnly, out var value) ? value : 0;
    }
}
