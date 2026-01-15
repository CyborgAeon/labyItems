namespace labyItems.Models.DTOs;

public sealed class CharacterDraft
{
    public Race? SelectedRace { get; set; }
    public Class? SelectedClass { get; set; }

    // Step 2
    public string Name { get; set; } = "";
    public string Background { get; set; } = "";
    public int Age { get; set; }

    // Step 3
    public HashSet<int> SelectedGuildIds { get; } = new();

    // Step 4
    public HashSet<int> SelectedBuffIds { get; } = new();

    // Derived / helper
    public bool IsRaceAndClassSelected => SelectedRace != null && SelectedClass != null;

    public bool CanNavigateToStep(int index)
        {
        return index switch
        {
            0 => true,
            1 => Draft.IsRaceAndClassSelected,
            2 => Draft.IsRaceAndClassSelected,
            3 => Draft.IsRaceAndClassSelected && IsStep2Valid(),
            4 => Draft.IsRaceAndClassSelected && IsStep2Valid(), // plus any other requirements
            _ => false
        };
        }
}