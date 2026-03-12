using System.Linq;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using SpecialisationCardPage = labyItems.Pages.SpecialisationCard.SpecialisationCard;

namespace labyItems.Pages.Characters;

public partial class CharacterSpecialisation : ContentView
{
    private CharacterSpecialisationVm? _boundVm;
    public ICommand ViewSpecialisationDetailsCommand { get; }
    public ICommand ViewSpecialisationAbilityDetailsCommand { get; }
    public ICommand ViewSpecialisationRowDetailsCommand { get; }

    public CharacterSpecialisation()
    {
        InitializeComponent();
        ViewSpecialisationDetailsCommand = new Command<object?>(parameter =>
            ViewSpecialisationDetails(parameter));
        ViewSpecialisationAbilityDetailsCommand = new Command<object?>(parameter =>
            ViewSpecialisationAbilityDetails(parameter));
        ViewSpecialisationRowDetailsCommand = new Command<object?>(parameter =>
            ViewSpecialisationRowDetails(parameter));
    }

    public CharacterSpecialisation(CharacterBuilderVm builderVm) : this()
    {
        BindingContext = builderVm.SpecialisationVm;
    }

    protected override void OnBindingContextChanged()
    {
        if (_boundVm != null && !ReferenceEquals(_boundVm, BindingContext))
            _boundVm.CancelReloads();

        base.OnBindingContextChanged();
        _boundVm = BindingContext as CharacterSpecialisationVm;
    }

    protected override void OnParentChanged()
    {
        base.OnParentChanged();

        if (Parent == null)
            _boundVm?.CancelReloads();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler == null)
            _boundVm?.CancelReloads();
    }

    private async void ViewSpecialisationAbilityDetails(object? parameter)
    {
        if (parameter is not SpecialisationSlotVm slot)
            return;

        var nav = ResolveNavigation();
        if (nav == null)
            return;

        var specialisationKey = (slot.SpecialisationKeyForDetails ?? string.Empty).Trim();
        if (specialisationKey.Length > 0)
        {
            var specialisationMatch = await DetailCardLookupService.FindSpecialisationAsync(specialisationKey);
            if (!string.IsNullOrWhiteSpace(specialisationMatch.Key) && specialisationMatch.Record != null)
            {
                await nav.PushAsync(new SpecialisationCardPage(
                    specialisationMatch.Key,
                    specialisationMatch.Record,
                    slot.SelectedOption));
                return;
            }
        }

        var abilityName = slot.SelectedAbilityNameForDetails;
        if (string.IsNullOrWhiteSpace(abilityName))
            return;

        var ability = await AbilityDetailsLookupService.FindByIndexAsync(abilityName);
        if (ability == null)
            return;

        await nav.PushAsync(new AbilityCardPage(ability));
    }

    private async void ViewSpecialisationDetails(object? parameter)
    {
        var key = string.Empty;
        var selectedOption = string.Empty;

        switch (parameter)
        {
            case CharacterSpecialisationVm vm:
                key = (vm.RaceSubtypeDetailKey ?? string.Empty).Trim();
                selectedOption = (vm.SelectedRaceSubtype ?? string.Empty).Trim();
                break;

            case MappedSpecialisationSectionVm mapped:
                key = (mapped.DetailKey ?? mapped.Key ?? string.Empty).Trim();
                selectedOption = (mapped.SelectedOption ?? string.Empty).Trim();
                break;

            case SpecialisationGroupVm group:
                key = (group.DetailKey ?? group.Title ?? string.Empty).Trim();
                break;

            case string keyText:
                key = keyText.Trim();
                break;
        }

        var nav = ResolveNavigation();
        if (key.Length == 0 || nav == null)
            return;

        var match = await DetailCardLookupService.FindSpecialisationAsync(key);
        if (string.IsNullOrWhiteSpace(match.Key) || match.Record == null)
            return;

        await nav.PushAsync(new SpecialisationCardPage(match.Key, match.Record, selectedOption));
    }

    private async void ViewSpecialisationRowDetails(object? parameter)
    {
        if (parameter is not SpecialisationAbilityRow row)
            return;

        var nav = ResolveNavigation();
        if (nav == null)
            return;

        var detailCandidates = new[]
        {
            (row.AbilityKey ?? string.Empty).Trim(),
            (row.SelectedAbility ?? string.Empty).Trim(),
            (row.Ability ?? string.Empty).Trim()
        }
        .Where(x => x.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        foreach (var candidate in detailCandidates)
        {
            var ability = await AbilityDetailsLookupService.FindByIndexAsync(candidate);
            if (ability == null)
                continue;

            await nav.PushAsync(new AbilityCardPage(ability));
            return;
        }

        foreach (var candidate in detailCandidates)
        {
            var abilityMatch = await DetailCardLookupService.FindSpecialisationAbilityAsync(candidate);
            if (abilityMatch.Ability == null)
                continue;

            await nav.PushAsync(new AbilityCardPage(ToAbilityResult(abilityMatch.Ability, abilityMatch.Key, candidate)));
            return;
        }

        var key = (row.SpecialisationKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return;

        var selectedOption = (row.SelectedAbility ?? row.Ability ?? row.SelectedOption ?? string.Empty).Trim();
        if (selectedOption.Length == 0)
            selectedOption = (row.SelectedOption ?? string.Empty).Trim();

        var match = await DetailCardLookupService.FindSpecialisationAsync(key);
        if (string.IsNullOrWhiteSpace(match.Key) || match.Record == null)
            return;

        await nav.PushAsync(new SpecialisationCardPage(match.Key, match.Record, selectedOption));
    }

    private INavigation? ResolveNavigation()
    {
        if (Navigation?.NavigationStack is { Count: > 0 })
            return Navigation;

        if (Shell.Current?.Navigation is { } shellNav)
            return shellNav;

        return Application.Current?.MainPage?.Navigation;
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
