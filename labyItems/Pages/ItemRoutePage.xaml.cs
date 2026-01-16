using labyItems.Pages.Calculator;

namespace labyItems.Pages;

public partial class ItemRoutePage : ContentPage
{
    public ItemRoutePage()
    {
        InitializeComponent();
    }

    private async void OnMakeCharacterClicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new labyItems.Pages.Characters.Wizard());
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
}
