using System.Text.Json;
using System.Text.Json.Serialization;
using SQLite;
using labyItems.Models.Enums;

namespace labyItems.Services;


public sealed class MiracleLookupService : ILookupService
{
    public async Task<IReadOnlyList<LookupItem>> GetAllAsync()
    {
        var all = await MiracleService.GetAllAsync();
        return all
            .Select(m => new LookupItem(Key: m.name, Display: $"{m.name} (P{m.power})"))
            .ToList();
    }

    public async Task<IReadOnlyList<LookupItem>> SearchAsync(string query)
    {
        var hits = await MiracleService.SearchAsync(query);
        return hits
            .Select(m => new LookupItem(Key: m.name, Display: $"{m.name} (P{m.power})"))
            .ToList();
    }
}

public static class MiracleService
{
    public sealed class MiracleDamageRaw
    {
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? amount { get; set; }
        public List<string>? type { get; set; }
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? ArmourApplies { get; set; }
        public string ArmourType { get; set; } = string.Empty;
        public List<int>? PACDam { get; set; }
    }

    public sealed class MiracleHealRaw
    {
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? amount { get; set; }
        public List<string>? type { get; set; }
    }

    public sealed record MiracRaw
    {
        public int power { get; set; } = 0;
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string verbal { get; set; } = string.Empty;
        public string range { get; set; } = string.Empty;
        public string duration { get; set; } = string.Empty;
        public string gesture { get; set; } = string.Empty;
        public string level { get; set; } = string.Empty;
        public string sphere { get; set; } = string.Empty;
        public bool isAdvanced { get; set; } = false;
        public string alignment { get; set; } = string.Empty;
        public bool nonStandard { get; set; }
        [JsonPropertyName("NonStandard")]
        public bool? NonStandardCompat { get; set; }

        [JsonPropertyName("Damage")]
        public MiracleDamageRaw? Damage { get; set; }

        [JsonPropertyName("Heal")]
        public MiracleHealRaw? Heal { get; set; }

        // Legacy miracle damage/heal fields retained for backward compatibility with older stored JSON.
        [JsonPropertyName("damage")]
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? damage { get; set; }
        [JsonPropertyName("damType")]
        public List<string>? damType { get; set; }
        [JsonPropertyName("sacApplies")]
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? sacApplies { get; set; }
        public string? damageOverride { get; set; }
        [JsonPropertyName("healing")]
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? healing { get; set; }
        [JsonPropertyName("healType")]
        public List<string>? healType { get; set; }
        [JsonPropertyName("preReqs")]
        public List<string>? preReqs { get; set; }

        public List<int[]> GetDamageAmounts()
            => Damage?.amount is { Count: > 0 } nested ? nested : (damage ?? new List<int[]>());

        public List<string> GetDamageTypes()
            => Damage?.type is { Count: > 0 } nested ? nested : (damType ?? new List<string>());

        public List<int[]> GetArmourApplies()
            => Damage?.ArmourApplies is { Count: > 0 } nested ? nested : (sacApplies ?? new List<int[]>());

        public string GetArmourType()
        {
            var token = (Damage?.ArmourType ?? string.Empty).Trim();
            if (token.Length > 0)
                return token.ToUpperInvariant();

            return (GetDamageAmounts().Count > 0 || !string.IsNullOrWhiteSpace(damageOverride))
                ? ArmourType.SAC.ToString()
                : string.Empty;
        }

        public ArmourType? GetArmourTypeEnum()
            => ArmourTypeParser.ParseOrNull(GetArmourType());

        public List<int> GetPacDamage()
            => Damage?.PACDam is { Count: > 0 } nested ? nested : new List<int>();

        public List<int[]> GetHealAmounts()
            => Heal?.amount is { Count: > 0 } nested ? nested : (healing ?? new List<int[]>());

        public List<string> GetHealTypes()
        {
            if (Heal?.type is { Count: > 0 } nested)
                return nested;

            if (healType is { Count: > 0 } legacy)
                return legacy;

            // Legacy typo compatibility: some older rows stored healing type in damType.
            if (GetHealAmounts().Count > 0
                && (damage == null || damage.Count == 0)
                && (sacApplies == null || sacApplies.Count == 0)
                && string.IsNullOrWhiteSpace(damageOverride)
                && damType is { Count: > 0 } typoLegacy)
            {
                return typoLegacy;
            }

            return new List<string>();
        }
    }
    private static List<MiracRaw>? _cache;
    private static readonly string? _dbPath = ResolveDbPath();

    public static async Task<IReadOnlyList<MiracRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        var dbList = await Task.Run(() => LoadFromDatabase());
        if (dbList is { Count: > 0 })
        {
            _cache = dbList;
            return _cache;
        }

        throw new InvalidOperationException("Miracles DB not found or empty; ensure laby.db is installed.");
    }

    private static List<MiracRaw>? LoadFromDatabase()
    {
        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
            return null;

        try
        {
            using var conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);
            var rows = conn.Query<DbRow>("SELECT data_json FROM miracles ORDER BY power, name;");
            var list = new List<MiracRaw>();
            foreach (var row in rows)
            {
                var e = JsonSerializer.Deserialize<MiracRaw>(row.data_json);
                if (e != null) list.Add(e);
            }

            return Normalize(list);
        }
        catch
        {
            return null;
        }
    }

    private static List<MiracRaw> Normalize(IEnumerable<MiracRaw> source) =>
        source
            .Select(e =>
            {
                e.name ??= string.Empty;
                e.description ??= string.Empty;
                e.verbal ??= string.Empty;
                e.range ??= string.Empty;
                e.duration ??= string.Empty;
                e.gesture ??= string.Empty;
                e.level ??= string.Empty;
                e.sphere ??= string.Empty;
                e.alignment ??= string.Empty;
                if (!e.nonStandard && e.NonStandardCompat == true)
                    e.nonStandard = true;
                e.damage ??= new List<int[]>();
                e.damType ??= new List<string>();
                e.sacApplies ??= new List<int[]>();
                e.healing ??= new List<int[]>();
                e.healType ??= new List<string>();
                e.preReqs ??= new List<string>();

                if (e.Damage != null)
                {
                    e.Damage.amount ??= new List<int[]>();
                    e.Damage.type ??= new List<string>();
                    e.Damage.ArmourApplies ??= new List<int[]>();
                    e.Damage.PACDam ??= new List<int>();
                    e.Damage.ArmourType = ArmourTypeParser.NormalizeOrEmpty(e.Damage.ArmourType);
                }

                if (e.Heal != null)
                {
                    e.Heal.amount ??= new List<int[]>();
                    e.Heal.type ??= new List<string>();
                }

                return e;
            })
            .OrderBy(e => e.power)
            .ThenBy(e => e.name)
            .ToList();

    public static async Task<IReadOnlyList<MiracRaw>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.name.ToLowerInvariant().Contains(query))
            .ToList();
    }

    public static void InvalidateCache()
        => _cache = null;

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
