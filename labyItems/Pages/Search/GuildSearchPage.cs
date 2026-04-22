using labyItems.Models.Characters;
using labyItems.Pages.Characters;
using labyItems.Controls;

namespace labyItems.Pages.Search;

public sealed class GuildSearchPage : ContentPage
{
    public GuildSearchPage()
    {
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);

        var vm = new GuildsVm(
            draft: new CharacterDraft(),
            notifyWizardGatingChanged: () => { },
            applyCharacterAvailabilityFilters: false,
            allowGuildSelection: false,
            searchByNameOnly: true,
            useMultiTypeFilters: true);

        var guilds = new Guilds(vm)
        {
            ShowBackButton = false,
            UseTypePills = true
        };

        var menu = new ToolsetNavigationMenu
        {
            SelectedRoute = ToolsetRouteKeys.Guilds
        };

        guilds.HeaderLeadingView = new BurgerMenuButton
        {
            Command = menu.OpenMenuCommand
        };

        var layout = new Grid
        {
            Children =
            {
                guilds,
                menu
            }
        };

        Content = layout;
    }
}
