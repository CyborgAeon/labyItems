namespace labyItems.Models.Characters;

public sealed class AbilityDraft
{
    public string? AbilityKey { get; set; }
    public string? GrantInstanceId { get; set; }
    public string? GrantDuration { get; set; }
    public List<AbilitySystemEffect>? GrantSystemEffects { get; set; }
    public bool HasConditionalArmour { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsActivatedGrant => GrantInstanceId != null
        && (AbilityType == AbilityType.Innate || (!string.IsNullOrWhiteSpace(GrantDuration)
            && !GrantDuration.Equals("Permanent", StringComparison.OrdinalIgnoreCase)));
    public string Name { get; set; } = "";
    public string? BattleboardNameOverride { get; set; }
    public string? UpdateKey { get; set; }
    public string ShortStringValue { get; set; } = "";
    public string? Effect { get; set; }
    public string? Source { get; set; }
    public int? Count { get; set; }
    public AbilityCountProgression? Progression { get; set; }
    public List<int>? Amount { get; set; }
    public string? Frequency { get; set; }
    public string? OverwriteKey { get; set; }
    public List<string> PreReqs { get; } = new();
    public List<string> GuildOverrides { get; } = new();

    public AbilityType AbilityType { get; set; }
    public int? ApplicableLevel { get; set; }
    public int? LevelGained { get; set; }
    public int? TableGained { get; set; }
}
