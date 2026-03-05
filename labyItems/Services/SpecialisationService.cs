using System.Text.Json;
using Android.App;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class SpecialisationService
{
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

        _cache = new Dictionary<string, SpecialisationRecord>(dict, StringComparer.OrdinalIgnoreCase);
        return _cache;
    }
}

public sealed class SpecialisationRecord
{
    public List<AbilityDefinition>? Abilities { get; set; }
    public PowerListRecord? PowerList { get; set; }

    // For tables like ElfColourAbilities
    public Dictionary<string, ColourAbilityRecord>? ColourAbilities { get; set; }
}

public sealed class ColourAbilityRecord
{
    public Dictionary<string, List<AbilityDefinition>>? Levels { get; set; }
    public string LifeScaleOverride { get; set; } = string.Empty;
    public string ArmourAvailabilityOverride { get; set; } = string.Empty;
    public List<string>? ColourChoiceOverride { get; set; }
    public GuildOverrideRules? GuildOverrides { get; set; }
    public List<string>? HedgeOrCircle { get; set; }
    public List<string>? ClassRestriction { get; set; }
}

public sealed class PowerListRecord
{
    public int Max { get; set; }
    public List<string>? Requirements { get; set; }
}
