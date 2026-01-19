namespace labyItems.Models.Characters;

public sealed class CharacterDraft
{
    public string? Race { get; set; }
    public string? Class { get; set; }
    public string Name { get; set; } = "";
    public string RaceSubtypeKey { get; set; }
    public string RaceSubtypeValue { get; set; }
    public string LifeScaleKeyOverride { get; set; }
    public string? RaceSubtype { get; set; }

    // NEW: optional multi-select guilds for the builder wizard
    public List<string> Guilds { get; set; } = new();

    public bool IsRaceAndClassSelected =>
        !string.IsNullOrWhiteSpace(Race) && !string.IsNullOrWhiteSpace(Class);
}
