using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Characters;
using labyItems.Models.Rules;

namespace labyItems.Services;

public static class MultiRaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static MultiRaceCatalog? _cache;

    public static async Task<MultiRaceCatalog> GetCatalogAsync()
    {
        if (_cache != null)
            return _cache;

        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("people/multi-races.json");
            _cache = JsonSerializer.Deserialize<MultiRaceCatalog>(json, JsonOptions)
                     ?? new MultiRaceCatalog();
        }
        catch
        {
            _cache = new MultiRaceCatalog();
        }

        return _cache;
    }

    public static void InvalidateCache()
        => _cache = null;
}

public sealed class MultiRaceCatalog
{
    [JsonPropertyName("metadata")]
    public JsonElement Metadata { get; set; }

    [JsonPropertyName("multiRaces")]
    public Dictionary<string, MultiRaceDefinition> MultiRaces { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MultiRaceDefinition :
    IMultiPathDefinition<MultiRaceAvailabilityOption, MultiRaceLevelAbility, MultiRaceSystemEffect>
{
    public string DisplayName { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public int MaxLevel { get; set; }
    public Dictionary<string, List<MultiRaceLevelAbility>> Levels { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public List<MultiRaceAvailabilityOption> AvailabilityOptions { get; set; } = new();
    public Dictionary<string, List<MultiRaceSystemEffect>> SystemEffectsByLevel { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Notes { get; set; } = new();
    public string? RequiresBracketPure { get; set; }
}

public sealed class MultiRaceLevelAbility : IMultiPathLevelAbility
{
    public string Name { get; set; } = string.Empty;
    public string AbilityRef { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public int? Count { get; set; }
    public AbilityCountProgression? Progression { get; set; }
    public List<string>? AsPerAbilityRefs { get; set; }
    public List<string>? PreReqs { get; set; }
    public List<string>? ChoiceSetRefs { get; set; }
}

public sealed class MultiRaceAvailabilityOption : IMultiPathAvailabilityOption
{
    public string Source { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public List<RuleClause> Rules { get; set; } = new();
    public Dictionary<string, int> CostsByLevel { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MultiRaceSystemEffect : IMultiPathSystemEffect<MultiRaceLifeScaleReference>
{
    public string EffectType { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
    public MultiRaceLifeScaleReference? LifeScaleReference { get; set; }
    public List<RuleClause> Conditions { get; set; } = new();
    public string SourceCategory { get; set; } = string.Empty;
    public List<string> LinkedAbilityRefs { get; set; } = new();
}

public sealed class MultiRaceLifeScaleReference : IMultiPathLifeScaleReference
{
    public string File { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int Level { get; set; }
}
