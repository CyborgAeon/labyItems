using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using SQLite;

namespace labyItems.Services;

public static class ClassService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new GuildOverrideRulesConverter() }
    };

    private static Dictionary<string, CharacterClassRecord>? _cache;

    public static async Task<Dictionary<string, CharacterClassRecord>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        try
        {
#if DEBUG
            using var s = await FileSystem.OpenAppPackageFileAsync("people/classes.json");
            using var r = new StreamReader(s);
            var json = await r.ReadToEndAsync();
            using var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var cls in doc.RootElement.EnumerateObject())
                {
                    if (cls.Value.ValueKind != JsonValueKind.Object)
                        continue;

                    dict[cls.Name] = DeserializeClassRecord(cls.Value);
                }
            }

            _cache = dict;
#else
            using var conn = ServiceHelper.OpenReadOnlyConnection();
            var rows = conn.Query<ClassRow>("SELECT name, data_json FROM classes ORDER BY name;");
            var dict = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.name))
                    continue;

                var record = string.IsNullOrWhiteSpace(row.data_json)
                    ? new CharacterClassRecord()
                    : DeserializeClassRecord(row.data_json);

                dict[row.name] = record;
            }

            _cache = dict;
#endif
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get classes", ex);
            throw;
        }

        return _cache;
    }

    private static CharacterClassRecord DeserializeClassRecord(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CharacterClassRecord();

        using var doc = JsonDocument.Parse(json);
        return DeserializeClassRecord(doc.RootElement);
    }

    private static CharacterClassRecord DeserializeClassRecord(JsonElement element)
    {
        var record = JsonSerializer.Deserialize<CharacterClassRecord>(element.GetRawText(), _jsonOptions)
                     ?? new CharacterClassRecord();

        record.Levels = MergeLevels(element, record.Levels);
        return record;
    }

    private static Dictionary<string, List<AbilityDefinition>> MergeLevels(
        JsonElement classElement,
        Dictionary<string, List<AbilityDefinition>>? existingLevels)
    {
        var merged = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase);

        if (existingLevels != null)
        {
            foreach (var kvp in existingLevels)
            {
                var key = (kvp.Key ?? string.Empty).Trim();
                if (key.Length == 0)
                    continue;

                merged[key] = kvp.Value?.Where(a => a != null).ToList() ?? new List<AbilityDefinition>();
            }
        }

        MergeLevelsFromProperty(classElement, "Levels", merged);
        MergeLevelsFromProperty(classElement, "levels", merged);

        return merged;
    }

    private static void MergeLevelsFromProperty(
        JsonElement classElement,
        string propertyName,
        Dictionary<string, List<AbilityDefinition>> target)
    {
        if (!classElement.TryGetProperty(propertyName, out var levelsElement)
            || levelsElement.ValueKind != JsonValueKind.Object)
            return;

        foreach (var level in levelsElement.EnumerateObject())
        {
            if (level.Value.ValueKind != JsonValueKind.Array)
                continue;

            var levelKey = (level.Name ?? string.Empty).Trim();
            if (levelKey.Length == 0)
                continue;

            var incoming = JsonSerializer.Deserialize<List<AbilityDefinition>>(level.Value.GetRawText(), _jsonOptions)
                           ?? new List<AbilityDefinition>();

            if (!target.TryGetValue(levelKey, out var existing))
            {
                target[levelKey] = incoming.Where(a => a != null).ToList();
                continue;
            }

            foreach (var ability in incoming)
            {
                if (ability == null)
                    continue;

                var alreadyPresent = existing.Any(a =>
                    string.Equals(a?.Name ?? string.Empty, ability.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(a?.Type ?? string.Empty, ability.Type ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(a?.OverwriteKey ?? string.Empty, ability.OverwriteKey ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                if (!alreadyPresent)
                    existing.Add(ability);
            }
        }
    }

    private sealed class ClassRow
    {
        public string name { get; set; } = string.Empty;
        public string? data_json { get; set; }
    }
}

public sealed class CharacterClassRecord
{
    public List<string> Brackets { get; set; } = new();
    public Dictionary<string, List<AbilityDefinition>> Levels { get; set; } = new();

    public ArmourAllowance? Armour { get; set; }

    [JsonPropertyName("Max AC")]
    public JsonElement MaxAC { get; set; }

    public List<string>? Powerbase { get; set; }

    public JsonElement PowerPerLevel { get; set; }

    public int? CasterLevel { get; set; }

    public List<PowerCalculation>? PowerCalculations { get; set; }

    [JsonPropertyName("Buy as")]
    public List<string>? BuyAs { get; set; }
    [JsonPropertyName("alignmentRule")]
    public AlignmentRule? AlignmentRule { get; set; }

    public GuildOverrideRules? GuildOverrides { get; set; }
}

public sealed class PowerCalculation
{
    public string PowerBase { get; set; } = "";
    public string Calculation { get; set; } = "";
}

public sealed class ArmourAllowance
{
    public List<string> Wearable { get; set; } = new();
    public List<string>? Restrictions { get; set; }
}
