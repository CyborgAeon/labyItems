using labyItems.Pages.Characters.ViewModels;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Characters;

public partial class AdvanceAbilitySearchPage : ContentPage
{
    private readonly AdvanceAbilitySearchVm _vm;
    private bool _initialized;

    public AdvanceAbilitySearchPage(AdvanceCharacterVm rootVm)
    {
        _vm = new AdvanceAbilitySearchVm(rootVm);
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
            return;

        _initialized = true;
        try
        {
            await _vm.LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ability Search", $"Failed to load abilities: {ex.Message}", "OK");
            await CloseAsync();
        }
    }

    private async void OnAbilityCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not AdvanceAbilitySearchItemVm ability)
            return;

        await Navigation.PushAsync(new AbilityCardPage(ability.Ability));
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _vm.ClearSelections();
        await CloseAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _vm.CommitSelection();
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (Navigation.NavigationStack.LastOrDefault() == this)
        {
            await Navigation.PopAsync();
            return;
        }

        if (Navigation.ModalStack.LastOrDefault() == this)
        {
            await Navigation.PopModalAsync();
            return;
        }

        if (Shell.Current != null)
            await Shell.Current.GoToAsync("..");
    }
}
