using System.Collections.ObjectModel;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class AdvanceAbilitySpecialisationAbilityVm
{
    public AdvanceAbilitySpecialisationAbilityVm(string abilityKey, string abilityName, int occurrence)
    {
        AbilityKey = (abilityKey ?? string.Empty).Trim();
        AbilityName = (abilityName ?? string.Empty).Trim();
        Occurrence = Math.Max(1, occurrence);
    }

    public string AbilityKey { get; }
    public string AbilityName { get; }
    public int Occurrence { get; }
    public string Subtitle => Occurrence > 1 ? $"Selection {Occurrence}" : "Selection";
    public ObservableCollection<ChoiceSetGroupVm> Groups { get; } = new();
}
