using labyItems.Models.Characters;
using labyItems.Pages.Characters;

namespace labyItems.Pages.Search;

public sealed class GuildSearchPage : ContentPage
{
    private bool _isNavigatingBack;

    public GuildSearchPage()
    {
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);

        var vm = new GuildsVm(
            draft: new CharacterDraft(),
            notifyWizardGatingChanged: () => { },
            applyCharacterAvailabilityFilters: false,
            allowGuildSelection: false,
            searchByNameOnly: true);

        Content = new Guilds(vm)
        {
            ShowBackButton = true,
            BackCommand = new Command(async () => await NavigateBackAsync())
        };
    }

    private async Task NavigateBackAsync()
    {
        if (_isNavigatingBack)
            return;

        _isNavigatingBack = true;
        try
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isNavigatingBack = false;
        }
    }
}
