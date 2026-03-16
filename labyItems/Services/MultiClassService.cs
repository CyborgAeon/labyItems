using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Characters;
using labyItems.Models.Rules;

namespace labyItems.Services;

public static class MultiClassService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static MultiClassCatalog? _cache;

    public static async Task<MultiClassCatalog> GetCatalogAsync()
    {
        if (_cache != null)
            return _cache;

        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("people/multi-classes.json");
            _cache = JsonSerializer.Deserialize<MultiClassCatalog>(json, JsonOptions)
                     ?? new MultiClassCatalog();
        }
        catch
        {
            _cache = new MultiClassCatalog();
        }

        return _cache;
    }

    public static void InvalidateCache()
        => _cache = null;
}

public sealed class MultiClassCatalog
{
    [JsonPropertyName("metadata")]
    public JsonElement Metadata { get; set; }

    [JsonPropertyName("multiClasses")]
    public Dictionary<string, MultiClassDefinition> MultiClasses { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MultiClassDefinition :
    IMultiPathDefinition<MultiClassAvailabilityOption, MultiClassLevelAbility, MultiClassSystemEffect>
{
    public string DisplayName { get; set; } = string.Empty;
    public int MaxLevel { get; set; }
    public Dictionary<string, List<MultiClassLevelAbility>> Levels { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public List<MultiClassAvailabilityOption> AvailabilityOptions { get; set; } = new();
    public Dictionary<string, List<MultiClassSystemEffect>> SystemEffectsByLevel { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("requiresBracketPure")]
    public string? RequiresBracketPure { get; set; }
}

public sealed class MultiClassLevelAbility : IMultiPathLevelAbility
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

public sealed class MultiClassAvailabilityOption : IMultiPathAvailabilityOption
{
    public string Source { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public List<RuleClause> Rules { get; set; } = new();
    public Dictionary<string, int> CostsByLevel { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MultiClassSystemEffect : IMultiPathSystemEffect<MultiClassLifeScaleReference>
{
    public string EffectType { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
    public MultiClassLifeScaleReference? LifeScaleReference { get; set; }
    public List<RuleClause> Conditions { get; set; } = new();
    public string SourceCategory { get; set; } = string.Empty;
    public List<string> LinkedAbilityRefs { get; set; } = new();
}

public sealed class MultiClassLifeScaleReference : IMultiPathLifeScaleReference
{
    public string File { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int Level { get; set; }
}
