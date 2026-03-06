using labyItems.Models.Characters;
using labyItems.Pages.Characters;

namespace labyItems.Pages.Search;

public sealed class GuildSearchPage : ContentPage
{
    public GuildSearchPage()
    {
        Title = "Guild Search";

        var vm = new GuildsVm(
            draft: new CharacterDraft(),
            notifyWizardGatingChanged: () => { },
            applyCharacterAvailabilityFilters: false,
            allowGuildSelection: false);

        Content = new Guilds(vm);
    }
}
