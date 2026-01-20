namespace labyItems.Models.Characters;

public sealed class AbilityDraft
{
    public string Name { get; set; } = "";
    public string ShortStringValue { get; set; } = ""; // what you called “short-string value”
}

public sealed class InnateAbilityDraft
{
    public string Name { get; set; } = "";
    public int Rank { get; set; } // 0..8
}
