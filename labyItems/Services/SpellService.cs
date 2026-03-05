using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using labyItems.Models.Enums;

namespace labyItems.Services;

public sealed class SpellLookupService : ILookupService
{
    public Task<IReadOnlyList<LookupItem>> GetAllAsync()
            => SearchAsync(query: "");

    public async Task<IReadOnlyList<LookupItem>> SearchAsync(string query)
    {
        var hits = await SpellService.SearchAsync(string.Empty);
        return hits
            .Select(e => new LookupItem(Key: e.name, Display: $"{e.name} ({e.level})"))
            .ToList();
    }
}

public static class SpellService
{
    public class SpellDamageRaw
    {
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? amount { get; set; }
        public List<string>? type { get; set; }
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? ArmourApplies { get; set; }
        public string ArmourType { get; set; } = string.Empty;
        public List<int>? PACDam { get; set; }
    }

    public class SpellRaw
    {
        public string name { get; set; } = string.Empty;
        public int level { get; set; } = 0;
        public string colour { get; set; } = string.Empty;
        public string range { get; set; } = string.Empty;
        public string duration { get; set; } = string.Empty;
        public string gesture { get; set; } = string.Empty;
        public string verbal { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string notes { get; set; } = string.Empty;
        [JsonPropertyName("Damage")]
        public SpellDamageRaw? Damage { get; set; }
        [JsonPropertyName("isAdvanced")]
        public bool? isAdvanced { get; set; } = false;
        [JsonPropertyName("IsAdvanced")]
        public bool? IsAdvancedCompat { get; set; }

        // Legacy spell damage fields retained for backward compatibility with older stored JSON.
        [JsonPropertyName("damage")]
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? legacyDamage { get; set; }
        public List<string>? damType { get; set; }
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? MACApplies { get; set; }
        public List<int>? PACDam { get; set; }
        public string? damageOverride { get; set; }

        public List<int[]> GetDamageAmounts()
            => Damage?.amount is { Count: > 0 } nested ? nested : (legacyDamage ?? new List<int[]>());

        public List<string> GetDamageTypes()
            => Damage?.type is { Count: > 0 } nested ? nested : (damType ?? new List<string>());

        public List<int[]> GetArmourApplies()
            => Damage?.ArmourApplies is { Count: > 0 } nested ? nested : (MACApplies ?? new List<int[]>());

        public List<int> GetPacDamage()
            => Damage?.PACDam is { Count: > 0 } nested ? nested : (PACDam ?? new List<int>());

        public string GetArmourType()
        {
            var value = (Damage?.ArmourType ?? string.Empty).Trim();
            return value.Length == 0 ? "MAC" : value.ToUpperInvariant();
        }

        public ArmourType? GetArmourTypeEnum()
            => ArmourTypeParser.ParseOrNull(GetArmourType());
    }

    private static List<SpellRaw>? _cache;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public static async Task<List<SpellRaw>> GetAllAsync()
    {
        if (_cache is { Count: > 0 })
            return _cache;

        var dbPath = ResolveDbPath();
        Debug.WriteLine($"[SPELLS] ResolveDbPath => {dbPath ?? "<none>"}");
        var dbList = LoadFromDatabase(dbPath);
        if (dbList is { Count: > 0 })
        {
            Debug.WriteLine($"[SPELLS] Loaded {dbList.Count} spells from DB.");
            _cache = dbList;
            return _cache;
        }

        var packaged = await LoadFromPackageAsync();
        if (packaged.Count > 0)
        {
            Debug.WriteLine($"[SPELLS] Loaded {packaged.Count} spells from packaged JSON.");
            _cache = packaged;
            return _cache;
        }

        Debug.WriteLine("[SPELLS] No spell records loaded from DB or package.");
        // Do not cache an empty result forever; allow a future retry after DB init or asset availability changes.
        _cache = null;
        return new List<SpellRaw>();
    }

    public static async Task<List<SpellRaw>> SearchAsync(string query, bool includeAdvanced = true, int? maxPower = null)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.name.ToLowerInvariant().Contains(query))
            .ToList();
    }

    private static List<SpellRaw>? LoadFromDatabase(string? dbPath)
    {
        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            return null;

        try
        {
            using var conn = new SQLite.SQLiteConnection(dbPath, SQLite.SQLiteOpenFlags.ReadOnly);
            var rows = conn.Query<DbRow>("SELECT data_json FROM spells;");
            var list = new List<SpellRaw>();
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.data_json))
                    continue;

                try
                {
                    var e = JsonSerializer.Deserialize<SpellRaw>(row.data_json, _jsonOptions);
                    if (e != null)
                        list.Add(Normalize(e));
                }
                catch
                {
                    // Skip malformed spell rows instead of failing the full spell catalog load.
                }
            }
            return list
                .OrderBy(s => s.level)
                .ThenBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SPELLS] DB load failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static async Task<List<SpellRaw>> LoadFromPackageAsync()
    {
        var candidates = new[]
        {
            "grimoire/allSpells.json",
            "Resources/Raw/grimoire/allSpells.json",
            "grimoire\\allSpells.json",
            "Resources\\Raw\\grimoire\\allSpells.json",
            "resources/raw/grimoire/allspells.json",
            "allSpells.json"
        };

        foreach (var candidate in candidates)
        {
            var loaded = await TryLoadFromPackagePathAsync(candidate);
            if (loaded.Count > 0)
                return loaded;
        }

        return new List<SpellRaw>();
    }

    private static async Task<List<SpellRaw>> TryLoadFromPackagePathAsync(string path)
    {
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync(path);
            try
            {
                var list = JsonSerializer.Deserialize<List<SpellRaw>>(json, _jsonOptions) ?? new List<SpellRaw>();
                var normalized = list
                    .Select(Normalize)
                    .OrderBy(spell => spell.level)
                    .ThenBy(spell => spell.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Debug.WriteLine($"[SPELLS] Package load success from '{path}' => {normalized.Count} rows.");
                return normalized;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SPELLS] Package parse failed for '{path}' as full list: {ex.GetType().Name}: {ex.Message}");
                var resilient = TryDeserializeSpellArrayResilient(json, path);
                if (resilient.Count > 0)
                    return resilient;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SPELLS] Package open failed for '{path}': {ex.GetType().Name}: {ex.Message}");
            return new List<SpellRaw>();
        }

        return new List<SpellRaw>();
    }

    private static List<SpellRaw> TryDeserializeSpellArrayResilient(string json, string sourcePath)
    {
        var list = new List<SpellRaw>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<SpellRaw>(element.GetRawText(), _jsonOptions);
                    if (parsed != null)
                        list.Add(Normalize(parsed));
                }
                catch
                {
                    // Skip malformed element and continue.
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SPELLS] Resilient JSON parse failed for '{sourcePath}': {ex.GetType().Name}: {ex.Message}");
            return new List<SpellRaw>();
        }

        var normalized = list
            .OrderBy(spell => spell.level)
            .ThenBy(spell => spell.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Debug.WriteLine($"[SPELLS] Resilient package parse from '{sourcePath}' => {normalized.Count} rows.");
        return normalized;
    }

    private static SpellRaw Normalize(SpellRaw raw)
    {
        raw.name = raw.name ?? string.Empty;
        raw.colour = raw.colour ?? string.Empty;
        raw.range = raw.range ?? string.Empty;
        raw.duration = raw.duration ?? string.Empty;
        raw.gesture = raw.gesture ?? string.Empty;
        raw.verbal = raw.verbal ?? string.Empty;
        raw.description = raw.description ?? string.Empty;
        raw.notes = raw.notes ?? string.Empty;
        raw.damType ??= new List<string>();
        raw.PACDam ??= new List<int>();
        raw.legacyDamage ??= new List<int[]>();
        raw.MACApplies ??= new List<int[]>();
        if (!raw.isAdvanced.HasValue && raw.IsAdvancedCompat.HasValue)
            raw.isAdvanced = raw.IsAdvancedCompat;

        if (raw.Damage != null)
        {
            raw.Damage.amount ??= new List<int[]>();
            raw.Damage.type ??= new List<string>();
            raw.Damage.ArmourApplies ??= new List<int[]>();
            raw.Damage.PACDam ??= new List<int>();
            raw.Damage.ArmourType ??= string.Empty;
        }

        return raw;
    }

    private sealed class DbRow
    {
        public string data_json { get; set; } = string.Empty;
    }

    private static string? ResolveDbPath()
    {
        var appDb = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
        if (File.Exists(appDb))
        {
            return appDb;
        }

        var devCandidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "output", "laby.db"),
        };

        foreach (var candidate in devCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed class IntArrayListConverter : JsonConverter<List<int[]>?>
    {
        public override List<int[]>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected array for int list.");

            var list = new List<int[]>();

            reader.Read();
            if (reader.TokenType == JsonTokenType.EndArray)
                return list;

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.TokenType == JsonTokenType.StartArray)
                {
                    var values = ReadIntArray(ref reader);
                    list.Add(values);
                    reader.Read();
                }

                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("Expected end of array.");

                return list;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                var values = new List<int>();
                while (reader.TokenType == JsonTokenType.Number)
                {
                    values.Add(reader.GetInt32());
                    reader.Read();
                }

                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("Expected end of array for int list.");

                list.Add(values.ToArray());
                return list;
            }

            throw new JsonException("Unexpected token for int list.");
        }

        public override void Write(Utf8JsonWriter writer, List<int[]>? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartArray();
            foreach (var arr in value)
            {
                writer.WriteStartArray();
                if (arr != null)
                {
                    foreach (var n in arr)
                        writer.WriteNumberValue(n);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }

        private static int[] ReadIntArray(ref Utf8JsonReader reader)
        {
            var values = new List<int>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.Number)
            {
                values.Add(reader.GetInt32());
                reader.Read();
            }

            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException("Expected end of array for int tuple.");

            return values.ToArray();
        }
    }
}
