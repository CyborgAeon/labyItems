namespace labyItems.Models.Characters;

public sealed class CharacterDraft
{
    public string? Race { get; set; }
    public string? Class { get; set; }
    public string Name { get; set; } = "";

    public bool IsRaceAndClassSelected =>
        !string.IsNullOrWhiteSpace(Race) && !string.IsNullOrWhiteSpace(Class);
}