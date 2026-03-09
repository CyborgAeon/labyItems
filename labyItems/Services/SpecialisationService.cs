using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class SpecialisationService
{
    private static readonly HashSet<string> _knownColourEntryFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Description",
        "Levels",
        "LifeScaleOverride",
        "ArmourAvailabilityOverride",
        "ColourChoiceOverride",
        "GuildOverrides",
        "HedgeOrCircle",
        "ClassRestriction"
    };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new GuildOverrideRulesConverter() }
    };

    private static Dictionary<string, SpecialisationRecord>? _cache;

    public static async Task<Dictionary<string, SpecialisationRecord>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        var json = await ServiceHelper.ReadPackageTextAsync("specialisation/specialisation.json");

        var dict = JsonSerializer.Deserialize<Dictionary<string, SpecialisationRecord>>(json, _jsonOptions)
                   ?? new Dictionary<string, SpecialisationRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in dict.Values)
            NormalizeRecord(record);

        _cache = new Dictionary<string, SpecialisationRecord>(dict, StringComparer.OrdinalIgnoreCase);
        return _cache;
    }

    private static void NormalizeRecord(SpecialisationRecord? record)
    {
        if (record == null)
            return;

        if (record.ColourAbilities is { Count: > 0 })
            return;

        var parsed = ParseColourAbilities(record.ExtraFields);
        if (parsed == null || parsed.Count == 0)
            return;

        record.ColourAbilities = parsed;

        if (record.ExtraFields == null)
            return;

        foreach (var key in parsed.Keys)
            record.ExtraFields.Remove(key);

        if (record.ExtraFields.Count == 0)
            record.ExtraFields = null;
    }

    private static Dictionary<string, ColourAbilityRecord>? ParseColourAbilities(Dictionary<string, JsonElement>? extraFields)
    {
        if (extraFields == null || extraFields.Count == 0)
            return null;

        var parsed = new Dictionary<string, ColourAbilityRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in extraFields)
        {
            if (!TryParseColourAbilityRecord(kvp.Value, out var entry))
                continue;

            parsed[kvp.Key] = entry;
        }

        return parsed.Count > 0 ? parsed : null;
    }

    private static bool TryParseColourAbilityRecord(JsonElement value, out ColourAbilityRecord entry)
    {
        entry = new ColourAbilityRecord();

        if (value.ValueKind == JsonValueKind.Array)
        {
            var list = ParseAbilityArray(value);
            if (list.Count == 0)
                return false;

            entry.Levels = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = list
            };
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
            return false;

        if (value.TryGetProperty("Description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
            entry.Description = descEl.GetString() ?? string.Empty;

        if (value.TryGetProperty("LifeScaleOverride", out var lifeEl) && lifeEl.ValueKind == JsonValueKind.String)
            entry.LifeScaleOverride = lifeEl.GetString() ?? string.Empty;

        if (value.TryGetProperty("ArmourAvailabilityOverride", out var armourEl) && armourEl.ValueKind == JsonValueKind.String)
            entry.ArmourAvailabilityOverride = armourEl.GetString() ?? string.Empty;

        if (value.TryGetProperty("ColourChoiceOverride", out var colourEl) && colourEl.ValueKind == JsonValueKind.Array)
        {
            entry.ColourChoiceOverride = colourEl
                .EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => x.Length > 0)
                .ToList();
        }

        if (value.TryGetProperty("GuildOverrides", out var guildEl))
            entry.GuildOverrides = GuildOverrideRulesConverter.FromElement(guildEl, _jsonOptions);

        if (value.TryGetProperty("HedgeOrCircle", out var hedgeEl) && hedgeEl.ValueKind == JsonValueKind.Array)
        {
            entry.HedgeOrCircle = hedgeEl
                .EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => x.Length > 0)
                .ToList();
        }

        if (value.TryGetProperty("ClassRestriction", out var classEl) && classEl.ValueKind == JsonValueKind.Array)
        {
            entry.ClassRestriction = classEl
                .EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => x.Length > 0)
                .ToList();
        }

        if (value.TryGetProperty("Levels", out var levelsEl) && levelsEl.ValueKind == JsonValueKind.Object)
            entry.Levels = ParseLevelArrays(levelsEl);
        else
            entry.Levels = ParseLevelArrays(value);

        var extras = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var hasLevelsProperty = value.TryGetProperty("Levels", out _);
        foreach (var prop in value.EnumerateObject())
        {
            if (_knownColourEntryFields.Contains(prop.Name))
                continue;

            if (!hasLevelsProperty && IsLevelKey(prop.Name) && prop.Value.ValueKind == JsonValueKind.Array)
                continue;

            extras[prop.Name] = prop.Value.Clone();
        }

        if (extras.Count > 0)
            entry.ExtraFields = extras;

        var hasLevels = entry.Levels is { Count: > 0 };
        var hasDescription = !string.IsNullOrWhiteSpace(entry.Description);
        var hasOverrides = !string.IsNullOrWhiteSpace(entry.LifeScaleOverride)
                           || !string.IsNullOrWhiteSpace(entry.ArmourAvailabilityOverride)
                           || entry.ColourChoiceOverride is { Count: > 0 }
                           || entry.HedgeOrCircle is { Count: > 0 }
                           || entry.ClassRestriction is { Count: > 0 }
                           || entry.GuildOverrides != null;

        return hasLevels || hasDescription || hasOverrides;
    }

    private static Dictionary<string, List<AbilityDefinition>> ParseLevelArrays(JsonElement levelsObject)
    {
        var levels = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase);
        if (levelsObject.ValueKind != JsonValueKind.Object)
            return levels;

        foreach (var lvlProp in levelsObject.EnumerateObject())
        {
            if (!IsLevelKey(lvlProp.Name))
                continue;

            if (lvlProp.Value.ValueKind != JsonValueKind.Array)
                continue;

            var list = ParseAbilityArray(lvlProp.Value);
            if (list.Count > 0)
                levels[lvlProp.Name] = list;
        }

        return levels;
    }

    private static bool IsLevelKey(string? key)
        => int.TryParse((key ?? string.Empty).Trim(), out _);

    private static List<AbilityDefinition> ParseAbilityArray(JsonElement array)
    {
        var list = new List<AbilityDefinition>();
        if (array.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in array.EnumerateArray())
        {
            var def = ParseAbilityDefinition(item);
            if (!string.IsNullOrWhiteSpace(def.Name))
                list.Add(def);
        }

        return list;
    }

    private static AbilityDefinition ParseAbilityDefinition(JsonElement element)
    {
        try
        {
            return JsonSerializer.Deserialize<AbilityDefinition>(element.GetRawText(), _jsonOptions)
                   ?? new AbilityDefinition();
        }
        catch
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => new AbilityDefinition { Name = element.GetString() ?? string.Empty },
                _ => new AbilityDefinition { Name = element.GetRawText() }
            };
        }
    }
}

public sealed class SpecialisationRecord
{
    public string Description { get; set; } = string.Empty;
    public List<AbilityDefinition>? Abilities { get; set; }
    public List<AbilityDefinition>? Options { get; set; }
    public PowerListRecord? PowerList { get; set; }

    // For tables like ElfColourAbilities
    public Dictionary<string, ColourAbilityRecord>? ColourAbilities { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }
}

public sealed class ColourAbilityRecord
{
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, List<AbilityDefinition>>? Levels { get; set; }
    public string LifeScaleOverride { get; set; } = string.Empty;
    public string ArmourAvailabilityOverride { get; set; } = string.Empty;
    public List<string>? ColourChoiceOverride { get; set; }
    public GuildOverrideRules? GuildOverrides { get; set; }
    public List<string>? HedgeOrCircle { get; set; }
    public List<string>? ClassRestriction { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }
}

public sealed class PowerListRecord
{
    public int Max { get; set; }
    public List<string>? Requirements { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }
}
