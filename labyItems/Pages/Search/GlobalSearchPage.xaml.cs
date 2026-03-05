using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;

namespace labyItems.Pages.Search;

public partial class GlobalSearchPage : ContentPage
{
    private readonly GlobalSearchVm _vm = new();

    public GlobalSearchPage()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.EnsureLoadedAsync();
    }

    private async void OnOpenDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not ImageButton button)
            return;

        if (button.CommandParameter is not GlobalSearchResultVm result)
            return;

        if (result.Spell != null)
        {
            await Navigation.PushAsync(new SpellCardPage(result.Spell));
            return;
        }

        if (result.Miracle != null)
            await Navigation.PushAsync(new MiracleCardPage(result.Miracle));
    }
}
