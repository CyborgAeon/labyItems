using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using labyItems.Models.Enums;

namespace labyItems.Services;

public static class DruidEvocationService
{
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
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? damage { get; set; }
        [JsonConverter(typeof(SingleOrArrayStringListConverter))]
        public List<string> damType { get; set; } = new();
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? healing { get; set; }
        [JsonConverter(typeof(SingleOrArrayStringListConverter))]
        public List<string> healType { get; set; } = new();
        public bool isAdvanced { get; set; }

        public List<int[]> GetDamageAmounts()
            => damage ?? new List<int[]>();

        public List<string> GetDamageTypes()
            => damType ?? new List<string>();

        public List<int[]> GetHealAmounts()
            => healing ?? new List<int[]>();

        public List<string> GetHealTypes()
            => healType ?? new List<string>();
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static List<EvocRaw>? _cache;

    public static async Task<IReadOnlyList<EvocRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        var json = await ServiceHelper.ReadPackageTextAsync("druids_way/evocs.json");

        var list = JsonSerializer.Deserialize<List<EvocRaw>>(json, _jsonOptions)
                   ?? new List<EvocRaw>();

        _cache = Normalize(list);
        return _cache;
    }

    private static List<EvocRaw> Normalize(IEnumerable<EvocRaw> source)
        => source
            .Select(e => new EvocRaw
            {
                name = e.name ?? string.Empty,
                power = e.power,
                fields = (e.fields ?? new List<string>())
                    .Select(f => (f ?? string.Empty).Trim())
                    .Where(f => f.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                description = e.description ?? string.Empty,
                range = e.range ?? string.Empty,
                duration = e.duration ?? string.Empty,
                verbal = e.verbal ?? string.Empty,
                preReqs = (e.preReqs ?? new List<string>())
                    .Select(p => (p ?? string.Empty).Trim())
                    .Where(p => p.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                damage = (e.damage ?? new List<int[]>()),
                damType = (e.damType ?? new List<string>())
                    .Select(t => DamTypeParser.NormalizeOrFallback(t, "Missile"))
                    .ToList(),
                healing = (e.healing ?? new List<int[]>()),
                healType = (e.healType ?? new List<string>())
                    .Select(t => DamTypeParser.NormalizeOrFallback(t, "Missile"))
                    .ToList(),
                isAdvanced = e.isAdvanced
            })
            .OrderBy(e => e.power)
            .ThenBy(e => e.name)
            .ToList();

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
                throw new JsonException("Expected end of nested int array.");

            return values.ToArray();
        }
    }
}
