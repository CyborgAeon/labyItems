using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Characters;

public partial class AdvanceAbilitySpecialisationPage : ContentPage
{
    private readonly AdvanceAbilitySpecialisationVm _vm;
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _initialized;
    private bool _completionSet;

    public AdvanceAbilitySpecialisationPage(
        CharacterDraft draft,
        IReadOnlyList<AdvanceAbilitySpecialisationRequest> requests)
    {
        _vm = new AdvanceAbilitySpecialisationVm(draft, requests);
        InitializeComponent();
        BindingContext = _vm;
        _vm.CloseRequested += OnCloseRequestedAsync;
    }

    public Task<bool> Completion => _completion.Task;

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
            await DisplayAlert("Ability Specialisation", $"Failed to load choice sets: {ex.Message}", "OK");
            await OnCloseRequestedAsync(false);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_completionSet)
            return;

        var stillOnNavigationStack = Navigation.NavigationStack.Contains(this);
        var stillOnModalStack = Navigation.ModalStack.Contains(this);
        if (stillOnNavigationStack || stillOnModalStack)
            return;

        _vm.RevertUnsavedSelections();
        TrySetCompletion(false);
    }

    private async Task OnCloseRequestedAsync(bool isSaved)
    {
        TrySetCompletion(isSaved);

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

    private void TrySetCompletion(bool value)
    {
        if (_completionSet)
            return;

        _completionSet = true;
        _completion.TrySetResult(value);
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
