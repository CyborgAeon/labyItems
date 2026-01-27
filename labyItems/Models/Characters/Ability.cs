namespace labyItems.Models.Characters;

public sealed class AbilityDraft
{
    public string Name { get; set; } = "";
    public string ShortStringValue { get; set; } = "";
    public string? Effect { get; set; }
    public string? Source { get; set; }
    public int? Count { get; set; }
    public List<int>? Amount { get; set; }
    public string? Frequency { get; set; }
    public string? OverwriteKey { get; set; }
    public List<string> PreReqs { get; } = new();
    public List<string> GuildOverrides { get; } = new();

    public AbilityType AbilityType { get; set; }
    public int? LevelGained { get; set; }
}

public enum AbilityType
{
    Immunity,
    Resistance,
    AtWill,
    Static,
    Pac,
    Dac,
    Mac,
    Sac,
    Life,
    WeaponSkill,
    Innate,
    Craft,
    Power,
    Overwrite,
    Replace,
    CastingList,
    GuildOverride,
    Update,
    LTM
}

public sealed class InnateAbilityDraft
{
    public string Name { get; set; } = "";
    public int Rank { get; set; }
}
