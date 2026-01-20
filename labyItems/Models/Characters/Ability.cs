namespace labyItems.Models.Characters;

public sealed class AbilityDraft
{
    public string Name { get; set; } = "";
    public string ShortStringValue { get; set; } = "";

    public AbilityType AbilityType { get; set; }
    public int? LevelGained { get; set; }
}

public enum AbilityType
{
    Immunity, Resistance, AtWill, Static, WeaponSkill, Innate
}

public sealed class InnateAbilityDraft
{
    public string Name { get; set; } = "";
    public int Rank { get; set; }
}
