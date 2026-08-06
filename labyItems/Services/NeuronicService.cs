using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Enums;
using SQLite;

namespace labyItems.Services;

public static class NeuronicService
{
    public sealed class NeuronicDamageRaw
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

    public sealed record NeuronicRaw
    {
        public int power { get; set; }
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string range { get; set; } = string.Empty;
        public string duration { get; set; } = string.Empty;
        [JsonConverter(typeof(SingleOrArrayStringConverter))]
        public string immunities { get; set; } = string.Empty;
        public string tree { get; set; } = string.Empty;
        public string notes { get; set; } = string.Empty;
        public string todo { get; set; } = string.Empty;
        public string asPer { get; set; } = string.Empty;

        [JsonPropertyName("Damage")]
        public NeuronicDamageRaw? Damage { get; set; }

        [JsonIgnore]
        public NeuroOptionType Type { get; set; } = NeuroOptionType.None;

        public List<int[]> GetDamageAmounts()
            => Damage?.amount is { Count: > 0 } nested ? nested : new List<int[]>();

        public List<string> GetDamageTypes()
            => Damage?.type is { Count: > 0 } nested ? nested : new List<string>();

        public List<int[]> GetArmourApplies()
            => Damage?.ArmourApplies is { Count: > 0 } nested ? nested : new List<int[]>();

        public List<int> GetPacDamage()
            => Damage?.PACDam is { Count: > 0 } nested ? nested : new List<int>();

        public string GetArmourType()
            => ArmourTypeParser.NormalizeOrEmpty(Damage?.ArmourType);

        public ArmourType? GetArmourTypeEnum()
            => ArmourTypeParser.ParseOrNull(GetArmourType());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static List<NeuronicRaw>? _cache;

    public static async Task<IReadOnlyList<NeuronicRaw>> GetAllAsync()
    {
        if (_cache is { Count: > 0 })
            return _cache;

        var loaded = await Task.Run(LoadFromDatabase);
        _cache = loaded;
        return _cache;
    }

    public static async Task<IReadOnlyList<NeuronicRaw>> SearchAsync(
        string query,
        NeuroOptionType? typeFilter = null)
    {
        var token = (query ?? string.Empty).Trim();
        var all = await GetAllAsync();

        return all
            .Where(n => typeFilter == null || n.Type == typeFilter.Value)
            .Where(n =>
                token.Length == 0
                || (n.name ?? string.Empty).Contains(token, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.power)
            .ThenBy(n => n.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void InvalidateCache()
    {
        _cache = null;
    }

    public static NeuroOptionType ParseType(string? raw)
    {
        var token = (raw ?? string.Empty).Trim();
        if (token.Equals(nameof(NeuroOptionType.Active), StringComparison.OrdinalIgnoreCase))
            return NeuroOptionType.Active;
        if (token.Equals(nameof(NeuroOptionType.Passive), StringComparison.OrdinalIgnoreCase))
            return NeuroOptionType.Passive;

        return NeuroOptionType.None;
    }

    public static string FormatType(NeuroOptionType type)
        => type switch
        {
            NeuroOptionType.Active => "Active",
            NeuroOptionType.Passive => "Passive",
            _ => "None"
        };

    private static List<NeuronicRaw> LoadFromDatabase()
    {
        var path = ServiceHelper.EnsureDbPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new List<NeuronicRaw>();

        try
        {
            using var conn = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
            var rows = conn.Query<NeuronicDbRow>("SELECT data_json, type FROM neuronics ORDER BY power, name;");
            var list = new List<NeuronicRaw>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.data_json))
                    continue;

                try
                {
                    var parsed = JsonSerializer.Deserialize<NeuronicRaw>(row.data_json, JsonOptions);
                    if (parsed != null)
                    {
                        if (string.IsNullOrWhiteSpace(parsed.tree) && !string.IsNullOrWhiteSpace(row.type))
                            parsed.tree = row.type;

                        list.Add(Normalize(parsed));
                    }
                }
                catch
                {
                    // Skip malformed rows so one bad custom/default record does not hide the whole catalogue.
                }
            }

            return list
                .OrderBy(n => n.power)
                .ThenBy(n => n.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NEURONICS] DB load failed: {ex.GetType().Name}: {ex.Message}");
            return new List<NeuronicRaw>();
        }
    }

    private static NeuronicRaw Normalize(NeuronicRaw raw)
    {
        raw.name = raw.name ?? string.Empty;
        raw.description = raw.description ?? string.Empty;
        raw.range = raw.range ?? string.Empty;
        raw.duration = raw.duration ?? string.Empty;
        raw.immunities = raw.immunities ?? string.Empty;
        raw.tree = raw.tree ?? string.Empty;
        raw.notes = raw.notes ?? string.Empty;
        raw.todo = raw.todo ?? string.Empty;
        raw.asPer = raw.asPer ?? string.Empty;
        raw.Type = ParseType(raw.tree);

        if (raw.Type != NeuroOptionType.None)
            raw.tree = FormatType(raw.Type);

        if (raw.Damage != null)
        {
            raw.Damage.amount = (raw.Damage.amount ?? new List<int[]>())
                .Select(ToIntPairArray)
                .ToList();
            raw.Damage.type = (raw.Damage.type ?? new List<string>())
                .Select(t => DamTypeParser.NormalizeOrFallback(t, "Missile"))
                .ToList();
            raw.Damage.ArmourApplies = (raw.Damage.ArmourApplies ?? new List<int[]>())
                .Select(ToIntPairArray)
                .ToList();
            raw.Damage.ArmourType = ArmourTypeParser.NormalizeOrEmpty(raw.Damage.ArmourType);
            raw.Damage.PACDam = (raw.Damage.PACDam ?? new List<int>())
                .Select(Math.Abs)
                .Where(v => v > 0)
                .ToList();
        }

        return raw;
    }

    private static int[] ToIntPairArray(int[]? values)
    {
        if (values == null || values.Length == 0)
            return new[] { 0, 0 };

        if (values.Length == 1)
            return new[] { Math.Abs(values[0]), 0 };

        return new[] { Math.Abs(values[0]), Math.Abs(values[1]) };
    }

    private sealed class NeuronicDbRow
    {
        public string data_json { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
    }

    private sealed class IntArrayListConverter : JsonConverter<List<int[]>?>
    {
        public override List<int[]>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            var result = new List<int[]>();
            if (reader.TokenType != JsonTokenType.StartArray)
                return result;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return result;

                if (reader.TokenType == JsonTokenType.StartArray)
                    result.Add(ReadIntArray(ref reader));
                else if (reader.TokenType == JsonTokenType.Number)
                    result.Add(new[] { ReadNumberAsInt(ref reader) });
                else
                    reader.Skip();
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, List<int[]>? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartArray();
            foreach (var pair in value)
            {
                writer.WriteStartArray();
                foreach (var number in pair ?? Array.Empty<int>())
                    writer.WriteNumberValue(number);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }

        private static int[] ReadIntArray(ref Utf8JsonReader reader)
        {
            var values = new List<int>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return values.ToArray();

                if (reader.TokenType == JsonTokenType.Number)
                    values.Add(ReadNumberAsInt(ref reader));
                else
                    reader.Skip();
            }

            return values.ToArray();
        }

        private static int ReadNumberAsInt(ref Utf8JsonReader reader)
        {
            if (reader.TryGetInt32(out var value))
                return value;

            if (reader.TryGetDouble(out var dbl))
                return (int)Math.Round(dbl);

            return 0;
        }
    }

    private sealed class SingleOrArrayStringConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString();

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                return null;
            }

            var values = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        values.Add(value.Trim());
                }
                else
                {
                    reader.Skip();
                }
            }

            return string.Join(", ", values);
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value);
        }
    }
}
