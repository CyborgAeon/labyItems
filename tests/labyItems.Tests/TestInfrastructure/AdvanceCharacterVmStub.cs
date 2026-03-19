using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class AdvanceCharacterVm
{
    public AdvanceCharacterVm(CharacterDraft? draft = null)
    {
        Draft = draft ?? new CharacterDraft();
    }

    public CharacterDraft Draft { get; }

    public List<EvolutionService.AbilityResult> AddedAbilities { get; } = new();

    public void AddAdvancementAbilities(IEnumerable<EvolutionService.AbilityResult>? abilities)
    {
        AddedAbilities.Clear();
        if (abilities == null)
            return;

        AddedAbilities.AddRange(abilities);
    }
}
