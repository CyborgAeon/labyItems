using System.Linq;
using labyItems.Pages.Calculator;
using labyItems.Pages.NonStandard;
using labyItems.Pages.Search;
using labyItems.Services;

namespace labyItems.Pages;

public partial class ItemRoutePage : ContentPage
{
    public ItemRoutePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshWalletButton();
    }

    private async void OnMakeCharacterClicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new labyItems.Pages.Characters.Wizard(null, async () => await Navigation.PopToRootAsync()));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Wizard failed", ex.ToString(), "OK");
        }
    }

    private async void OnCreateIspClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new CharactersPage());
    }

    private async void OnCreateMpClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new MpCalculator());
    }

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new GlobalSearchPage());
    }

    private async void OnGuildSearchClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new GuildSearchPage());
    }

    private async void OnCreateNonStandardClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new NonStandardCreatePage());
    }

    private async void OnCharacterWalletClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new Characters.CharacterWalletPage());
    }

    private void RefreshWalletButton()
    {
        try
        {
            var any = LiteDbService.GetCharacters().Any();
            if (CharacterWalletButton != null)
            {
                CharacterWalletButton.IsEnabled = any;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WALLET_BUTTON] {ex}");
            if (CharacterWalletButton != null)
            {
                CharacterWalletButton.IsEnabled = false;
            }
        }
    }
}
