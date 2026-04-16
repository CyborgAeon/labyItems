using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using SpecialisationCardPage = labyItems.Pages.SpecialisationCard.SpecialisationCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Characters;

public partial class MultiRaceSpecialisationPage : ContentPage
{
    private readonly MultiRaceSpecialisationVm _vm;
    private bool _initialized;

    public MultiRaceSpecialisationPage(CharacterDraft draft)
    {
        _vm = new MultiRaceSpecialisationVm(draft);
        InitializeComponent();
        _vm.CloseRequested += OnCloseRequestedAsync;
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
            await DisplayAlert("Multi-Race Specialisation", $"Failed to load multi-race choices: {ex.Message}", "OK");
            await OnCloseRequestedAsync();
        }
    }

    private async Task OnCloseRequestedAsync()
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

    private async void OnAbilityInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button
            || button.CommandParameter is not ChoiceSetAbilityRowVm row)
        {
            return;
        }

        var options = _vm.GetAbilityDetailsForRow(row)
            .Where(option => option.IsValid)
            .ToList();
        if (options.Count == 0)
            return;

        var selected = options[0];
        var detail = await DetailCardLookupService.ResolveDetailAsync(selected.LookupKey, selected.DisplayName);
        if (detail?.Ability != null)
        {
            await Navigation.PushAsync(new AbilityCardPage(detail.Ability));
            return;
        }

        if (detail?.Specialisation != null)
        {
            await Navigation.PushAsync(new SpecialisationCardPage(detail.Key, detail.Specialisation));
            return;
        }

        await DisplayAlert("No Ability Card", $"Could not find a detail card for \"{selected.DisplayName}\".", "OK");
    }
}
