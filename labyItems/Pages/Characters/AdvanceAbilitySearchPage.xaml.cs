using labyItems.Pages.Characters.ViewModels;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Characters;

public partial class AdvanceAbilitySearchPage : ContentPage
{
    private readonly AdvanceAbilitySearchVm _vm;
    private bool _initialized;
    private bool _disposed;

    public AdvanceAbilitySearchPage(AdvanceCharacterVm rootVm)
    {
        _vm = new AdvanceAbilitySearchVm(rootVm);
        InitializeComponent();
        BindingContext = _vm;
    }

    public AdvanceAbilitySearchPage(IAbilitySearchHost host)
    {
        _vm = new AdvanceAbilitySearchVm(host);
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_disposed)
            return;

        var stillOnNavigationStack = Navigation.NavigationStack.Contains(this);
        var stillOnModalStack = Navigation.ModalStack.Contains(this);
        if (stillOnNavigationStack || stillOnModalStack)
            return;

        DisposeVm();
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

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (_vm.AllowSpecialisationSelection)
        {
            var preReqCheck = _vm.GetSelectionPrerequisiteIssues();
            if (preReqCheck.HasIssues)
            {
                var message = _vm.BuildMissingPrerequisiteMessage(preReqCheck);
                var shouldView = await DisplayAlert(
                    "Missing prerequisites",
                    message,
                    "View",
                    "Cancel");

                if (shouldView)
                    _vm.FocusMissingPrerequisites(preReqCheck);
                return;
            }

            var selectedAbilities = _vm.GetSelectedAbilities();
            var specialisationRequests = await _vm.BuildSpecialisationRequestsAsync(selectedAbilities);
            if (specialisationRequests.Count > 0)
            {
                var specialisationPage = new AdvanceAbilitySpecialisationPage(_vm.Draft, specialisationRequests);
                await Navigation.PushAsync(specialisationPage);
                var shouldCommit = await specialisationPage.Completion;
                if (!shouldCommit)
                    return;
            }
        }

        _vm.CommitSelection(_vm.GetSelectedAbilities());
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        DisposeVm();

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

    private void DisposeVm()
    {
        if (_disposed)
            return;

        _disposed = true;
        _vm.Dispose();
    }
}
