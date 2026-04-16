using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using SpecialisationCardPage = labyItems.Pages.SpecialisationCard.SpecialisationCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Characters;

public partial class MultiRaceWizardPage : ContentPage
{
    private readonly MultiRaceWizardVm _vm;
    private readonly CharacterDraft _draft;
    private bool _initialized;

    public ICommand SelectClassCommand => _vm.SelectClassCommand;
    public ICommand ToggleExpandedCommand => _vm.ToggleExpandedCommand;
    public ICommand ToggleFilterChipCommand => _vm.ToggleFilterChipCommand;

    public MultiRaceWizardPage(
        CharacterDraft draft,
        string? preselectedMultiRaceKey = null,
        bool openDetailStep = false)
    {
        _draft = draft;
        _vm = new MultiRaceWizardVm(
            draft,
            initialStorageKey: preselectedMultiRaceKey,
            openDetailOnLoad: openDetailStep);
        InitializeComponent();
        _vm.CloseRequested += OnCloseRequestedAsync;
        _vm.OpenSpecialisationRequested += OnOpenSpecialisationRequestedAsync;
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
            await DisplayAlert("Multi-Race Wizard", $"Failed to load multi-races: {ex.Message}", "OK");
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

    private async Task OnOpenSpecialisationRequestedAsync()
    {
        var specialisationPage = new MultiRaceSpecialisationPage(_draft);
        if (Navigation.ModalStack.LastOrDefault() == this)
        {
            await Navigation.PopModalAsync();
            await (Shell.Current?.Navigation ?? Navigation).PushAsync(specialisationPage);
            return;
        }

        var nav = Shell.Current?.Navigation ?? Navigation;
        await nav.PushAsync(specialisationPage);

        if (nav.NavigationStack.Contains(this))
            nav.RemovePage(this);
        else if (Navigation.NavigationStack.Contains(this))
            Navigation.RemovePage(this);
    }

    private async void OnLevelInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not MultiRaceLevelRowVm row)
            return;

        var options = _vm.GetAbilityDetailsForRow(row)
            .Where(option => option.IsValid)
            .ToList();
        if (options.Count == 0)
            return;

        MultiRaceAbilityDetailVm? selected = null;
        if (options.Count == 1)
        {
            selected = options[0];
        }
        else
        {
            var labels = options.Select(option => option.DisplayName).ToArray();
            var picked = await DisplayActionSheet($"Level {row.Level} abilities", "Cancel", null, labels);
            if (string.IsNullOrWhiteSpace(picked) || picked.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
                return;

            selected = options.FirstOrDefault(option =>
                option.DisplayName.Equals(picked, StringComparison.OrdinalIgnoreCase));
        }

        if (selected == null)
            return;

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
