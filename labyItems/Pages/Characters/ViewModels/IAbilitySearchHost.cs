using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Pages.Characters.ViewModels;

public interface IAbilitySearchHost
{
    CharacterDraft Draft { get; }
    bool AllowSpecialisationSelection { get; }
    bool DefaultAvailableOnly { get; }
    IReadOnlyList<string> DefaultSourceBookFilters => Array.Empty<string>();
    string TitleText { get; }
    string SubtitleText { get; }
    string ConfirmButtonText { get; }
    void CommitSelection(IEnumerable<EvolutionService.AbilityResult>? selectedAbilities);
}
