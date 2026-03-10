using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using labyItems.Models.Enums;

namespace labyItems.Services;

public static class DruidEvocationService
{
    public sealed class EvocationDamageRaw
    {
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? amount { get; set; }

        [JsonConverter(typeof(SingleOrArrayStringListConverter))]
        public List<string>? type { get; set; }

        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? ArmourApplies { get; set; }

        public string ArmourType { get; set; } = string.Empty;

        public List<int>? PACDam { get; set; }
    }

    public sealed class EvocationHealRaw
    {
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? amount { get; set; }

        [JsonConverter(typeof(SingleOrArrayStringListConverter))]
        public List<string>? type { get; set; }
    }

    public sealed record EvocRaw
    {
        public string name { get; set; } = string.Empty;
        public int power { get; set; }
        public List<string> fields { get; set; } = new();
        public string description { get; set; } = string.Empty;
        public string range { get; set; } = string.Empty;
        public string duration { get; set; } = string.Empty;
        public string verbal { get; set; } = string.Empty;
        public List<string> preReqs { get; set; } = new();

        [JsonPropertyName("Damage")]
        public EvocationDamageRaw? Damage { get; set; }

        [JsonPropertyName("Heal")]
        public EvocationHealRaw? Heal { get; set; }

        // Legacy fields retained for compatibility with older JSON/DB rows.
        [JsonPropertyName("damage")]
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? damage { get; set; }

        [JsonPropertyName("damType")]
        [JsonConverter(typeof(SingleOrArrayStringListConverter))]
        public List<string>? damType { get; set; }

        [JsonPropertyName("InnatePacApplies")]
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? InnatePacApplies { get; set; }

        [JsonPropertyName("healing")]
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? healing { get; set; }

        [JsonPropertyName("healType")]
        [JsonConverter(typeof(SingleOrArrayStringListConverter))]
        public List<string>? healType { get; set; }

        public bool isAdvanced { get; set; }
        public bool nonStandard { get; set; }
        [JsonPropertyName("NonStandard")]
        public bool? NonStandardCompat { get; set; }

        public List<int[]> GetDamageAmounts()
            => Damage?.amount is { Count: > 0 } nested ? nested : (damage ?? new List<int[]>());

        public List<string> GetDamageTypes()
            => Damage?.type is { Count: > 0 } nested ? nested : (damType ?? new List<string>());

        public List<int[]> GetArmourApplies()
            => Damage?.ArmourApplies is { Count: > 0 } nested ? nested : (InnatePacApplies ?? new List<int[]>());

        public string GetArmourType()
        {
            var token = ArmourTypeParser.NormalizeOrEmpty(Damage?.ArmourType);
            if (!string.IsNullOrWhiteSpace(token))
                return token;

            return GetArmourApplies().Count > 0
                ? ArmourType.InnatePac.ToString()
                : string.Empty;
        }

        public ArmourType? GetArmourTypeEnum()
            => ArmourTypeParser.ParseOrNull(GetArmourType());

        public List<int> GetPacDamage()
            => Damage?.PACDam is { Count: > 0 } nested ? nested : new List<int>();

        public List<int[]> GetHealAmounts()
            => Heal?.amount is { Count: > 0 } nested ? nested : (healing ?? new List<int[]>());

        public List<string> GetHealTypes()
            => Heal?.type is { Count: > 0 } nested ? nested : (healType ?? new List<string>());
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private static List<EvocRaw>? _cache;

    public static async Task<IReadOnlyList<EvocRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        var merged = new Dictionary<string, EvocRaw>(StringComparer.OrdinalIgnoreCase);

        var json = await ReadPackagedJsonAsync();
        var packaged = JsonSerializer.Deserialize<List<EvocRaw>>(json, _jsonOptions)
                       ?? new List<EvocRaw>();
        foreach (var item in packaged)
        {
            if (string.IsNullOrWhiteSpace(item?.name))
                continue;

            merged[item.name.Trim()] = item;
        }

        foreach (var item in LoadFromDatabase())
        {
            if (string.IsNullOrWhiteSpace(item?.name))
                continue;

            merged[item.name.Trim()] = item;
        }

        _cache = Normalize(merged.Values);
        return _cache;
    }

    public static void InvalidateCache()
        => _cache = null;

    private static async Task<string> ReadPackagedJsonAsync()
    {
        Exception? last = null;
        var paths = new[]
        {
            "druids_way/evocs.json",
            "Resources/Raw/druids_way/evocs.json"
        };

        foreach (var path in paths)
        {
            try
            {
                return await ServiceHelper.ReadPackageTextAsync(path);
            }
            catch (FileNotFoundException ex)
            {
                last = ex;
            }
            catch (DirectoryNotFoundException ex)
            {
                last = ex;
            }
        }

        if (last != null)
            throw last;

        throw new FileNotFoundException("Unable to locate packaged evocations JSON.");
    }

    private static List<EvocRaw> Normalize(IEnumerable<EvocRaw> source)
        => source
            .Select(NormalizeEvocation)
            .OrderBy(e => e.power)
            .ThenBy(e => e.name)
            .ToList();

    private static List<EvocRaw> LoadFromDatabase()
    {
        var path = ServiceHelper.EnsureDbPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new List<EvocRaw>();

        try
        {
            using var conn = new SQLite.SQLiteConnection(path, SQLite.SQLiteOpenFlags.ReadOnly);
            var rows = conn.Query<EvocationDbRow>("SELECT data_json FROM evocs ORDER BY name;");
            var list = new List<EvocRaw>();
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.data_json))
                    continue;

                try
                {
                    var parsed = JsonSerializer.Deserialize<EvocRaw>(row.data_json, _jsonOptions);
                    if (parsed != null)
                        list.Add(parsed);
                }
                catch
                {
                    // skip malformed rows
                }
            }

            return list;
        }
        catch
        {
            return new List<EvocRaw>();
        }
    }

    private static EvocRaw NormalizeEvocation(EvocRaw source)
    {
        var damageAmounts = (source.GetDamageAmounts() ?? new List<int[]>())
            .Select(ToIntPairArray)
            .ToList();
        var damageTypes = (source.GetDamageTypes() ?? new List<string>())
            .Select(t => DamTypeParser.NormalizeOrFallback(t, "Missile"))
            .ToList();
        var armourApplies = (source.GetArmourApplies() ?? new List<int[]>())
            .Select(ToIntPairArray)
            .ToList();
        var armourType = ArmourTypeParser.NormalizeOrEmpty(source.GetArmourType());
        if (string.IsNullOrWhiteSpace(armourType) && armourApplies.Count > 0)
            armourType = ArmourType.InnatePac.ToString();

        var healAmounts = (source.GetHealAmounts() ?? new List<int[]>())
            .Select(ToIntPairArray)
            .ToList();
        var healTypes = (source.GetHealTypes() ?? new List<string>())
            .Select(t => DamTypeParser.NormalizeOrFallback(t, "Worst"))
            .ToList();
        var pacDamage = (source.GetPacDamage() ?? new List<int>())
            .Select(Math.Abs)
            .Where(v => v > 0)
            .ToList();

        var normalizedDamage = (damageAmounts.Count > 0 || damageTypes.Count > 0 || armourApplies.Count > 0 || pacDamage.Count > 0)
            ? new EvocationDamageRaw
            {
                amount = damageAmounts,
                type = damageTypes,
                ArmourApplies = armourApplies,
                ArmourType = armourType,
                PACDam = pacDamage
            }
            : null;

        var normalizedHeal = (healAmounts.Count > 0 || healTypes.Count > 0)
            ? new EvocationHealRaw
            {
                amount = healAmounts,
                type = healTypes
            }
            : null;

        var legacyInnateApplies = (armourType.Equals(ArmourType.InnatePac.ToString(), StringComparison.OrdinalIgnoreCase))
            ? armourApplies
            : new List<int[]>();

        return new EvocRaw
        {
            name = source.name ?? string.Empty,
            power = source.power,
            fields = (source.fields ?? new List<string>())
                .Select(f => (f ?? string.Empty).Trim())
                .Where(f => f.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            description = source.description ?? string.Empty,
            range = source.range ?? string.Empty,
            duration = source.duration ?? string.Empty,
            verbal = source.verbal ?? string.Empty,
            preReqs = (source.preReqs ?? new List<string>())
                .Select(p => (p ?? string.Empty).Trim())
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Damage = normalizedDamage,
            Heal = normalizedHeal,
            damage = damageAmounts,
            damType = damageTypes,
            InnatePacApplies = legacyInnateApplies,
            healing = healAmounts,
            healType = healTypes,
            isAdvanced = source.isAdvanced,
            nonStandard = source.nonStandard || source.NonStandardCompat == true
        };
    }

    private sealed class EvocationDbRow
    {
        public string data_json { get; set; } = string.Empty;
    }

    private static int[] ToIntPairArray(int[]? values)
    {
        if (values == null || values.Length == 0)
            return new[] { 0, 0 };

        if (values.Length == 1)
            return new[] { Math.Max(0, values[0]), 0 };

        return new[] { Math.Max(0, values[0]), Math.Max(0, values[1]) };
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
                    values.Add(ReadNumberAsInt(ref reader));
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
                values.Add(ReadNumberAsInt(ref reader));
                reader.Read();
            }

            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException("Expected end of nested int array.");

            return values.ToArray();
        }

        private static int ReadNumberAsInt(ref Utf8JsonReader reader)
        {
            if (reader.TryGetInt32(out var value))
                return value;

            if (reader.TryGetDouble(out var dbl))
                return (int)Math.Truncate(dbl);

            throw new JsonException("Invalid numeric token in int array.");
        }
    }
}
