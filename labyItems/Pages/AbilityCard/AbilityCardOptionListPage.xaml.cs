using System.Collections.ObjectModel;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.AbilityCard;

public partial class AbilityCardOptionListPage : ContentPage
{
    public ObservableCollection<ChoiceSetAbilityRowVm> Options { get; } = new();
    public string TitleText { get; private set; }
    public bool ShowHeaderDescription => Options.Count > 0;

    public AbilityCardOptionListPage(string title, IReadOnlyList<ChoiceSetAbilityRowVm> options)
    {
        InitializeComponent();
        Title = title;
        TitleText = title;
        foreach (var option in options)
        {
            Options.Add(option);
        }

        BindingContext = this;
    }

    private async void OnInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ChoiceSetAbilityRowVm option)
            return;

        var ability = await ResolveAbilityAsync(option.LookupKey);
        if (ability == null
            && !string.Equals(option.LookupKey, option.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            ability = await ResolveAbilityAsync(option.DisplayName);
        }

        if (ability == null)
        {
            await DisplayAlert("No Ability Card", $"Could not find a detail card for \"{option.DisplayName}\".", "OK");
            return;
        }

        await Navigation.PushAsync(new AbilityCardPage(ability));
    }

    private static async Task<EvolutionService.AbilityResult?> ResolveAbilityAsync(string selectedAbility)
    {
        var ability = await AbilityDetailsLookupService.FindByIndexAsync(selectedAbility);
        if (ability != null)
            return ability;

        var specialisationAbility = await DetailCardLookupService.FindSpecialisationAbilityAsync(selectedAbility);
        if (specialisationAbility.Ability == null)
            return null;

        return ToAbilityResult(
            specialisationAbility.Ability,
            specialisationAbility.Key,
            selectedAbility);
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
