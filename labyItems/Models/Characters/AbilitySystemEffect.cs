namespace labyItems.Models.Characters;

public sealed class AbilitySystemEffect
{
    public string EffectType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ResistanceType { get; set; }
    public int? Level { get; set; }
    public string? ImmunityName { get; set; }
}
