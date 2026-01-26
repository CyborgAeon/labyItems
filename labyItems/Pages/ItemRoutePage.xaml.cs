using System.Linq;
using labyItems.Pages.Calculator;
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

    private async void OnCharacterWalletClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new Characters.CharacterWalletPage());
    }

    private void RefreshWalletButton()
    {
        var any = LiteDbService.GetCharacters().Any();
        CharacterWalletButton.IsEnabled = any;
    }
}
