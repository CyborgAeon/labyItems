using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
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
        var openSpecialisation = _vm.OpenSpecialisationAfterClose;
        var specialisationPage = openSpecialisation
            ? new MultiRaceSpecialisationPage(_draft)
            : null;
        if (Navigation.NavigationStack.LastOrDefault() == this)
        {
            if (specialisationPage != null)
            {
                await Navigation.PushAsync(specialisationPage);
                Navigation.RemovePage(this);
            }
            else
            {
                await Navigation.PopAsync();
            }
            return;
        }

        if (Navigation.ModalStack.LastOrDefault() == this)
        {
            await Navigation.PopModalAsync();
            if (specialisationPage != null)
                await Navigation.PushAsync(specialisationPage);
            return;
        }

        if (Shell.Current != null)
            await Shell.Current.GoToAsync("..");
        if (specialisationPage != null)
            await (Shell.Current?.Navigation ?? Navigation).PushAsync(specialisationPage);
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

        var ability = await ResolveAbilityAsync(selected.LookupKey);
        if (ability == null
            && !string.Equals(selected.LookupKey, selected.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            ability = await ResolveAbilityAsync(selected.DisplayName);
        }

        if (ability == null)
        {
            await DisplayAlert("No Ability Card", $"Could not find a detail card for \"{selected.DisplayName}\".", "OK");
            return;
        }

        await Navigation.PushAsync(new AbilityCardPage(ability));
    }

    private static async Task<EvolutionService.AbilityResult?> ResolveAbilityAsync(string key)
    {
        var byIndex = await AbilityDetailsLookupService.FindByIndexAsync(key);
        if (byIndex != null)
            return byIndex;

        var fromSpecialisation = await DetailCardLookupService.FindSpecialisationAbilityAsync(key);
        if (fromSpecialisation.Ability == null)
            return null;

        return ToAbilityResult(fromSpecialisation.Ability, fromSpecialisation.Key, key);
    }

    private static EvolutionService.AbilityResult ToAbilityResult(
        AbilityDefinition source,
        string resolvedKey,
        string requestedKey)
    {
        var name = (source.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (resolvedKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (requestedKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = "Ability";

        return new EvolutionService.AbilityResult
        {
            Index = name,
            Description = source.Effect ?? string.Empty,
            Cost = 0,
            Table = 0,
            Available = source.Source ?? "ALL",
            CanBuyMultiple = false,
            PreReqs = source.PreReqs is { Count: > 0 } preReqs
                ? preReqs
                : Array.Empty<string>(),
            MaxAvailable = source.Count
        };
    }
}
