using labyItems.Models.Characters;

namespace labyItems.Pages.Characters.ViewModels;

public static class WizardStepRules
{
    public static bool CanEnterSpecialisation(CharacterDraft draft)
        => draft.IsRaceAndClassSelected;

    public static bool CanEnterGuilds(CharacterDraft draft, CharacterSpecialisationVm specialisationVm)
        => draft.IsRaceAndClassSelected && specialisationVm.IsComplete;

    public static bool CanEnterDetails(CharacterDraft draft, CharacterSpecialisationVm specialisationVm, GuildsVm guildsVm)
        => CanEnterGuilds(draft, specialisationVm) && guildsVm.IsComplete;

    public static bool CanEnterReview(CharacterDraft draft, CharacterSpecialisationVm specialisationVm, GuildsVm guildsVm)
        => CanEnterDetails(draft, specialisationVm, guildsVm);
}
