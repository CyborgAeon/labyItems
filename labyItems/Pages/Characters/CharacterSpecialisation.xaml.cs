using System.Windows.Input;
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
            var specialisationLookup = await SpecialisationService.GetAllAsync();
            var specialisationMatch = specialisationLookup.FirstOrDefault(kvp =>
                string.Equals(kvp.Key, specialisationKey, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(specialisationMatch.Key) && specialisationMatch.Value != null)
            {
                await nav.PushAsync(new SpecialisationCardPage(
                    specialisationMatch.Key,
                    specialisationMatch.Value,
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

            case CharacterSpecialisationVm.MappedSpecialisationVm mapped:
                key = (mapped.Key ?? string.Empty).Trim();
                selectedOption = (mapped.SelectedOption ?? string.Empty).Trim();
                break;

            case SpecialisationGroupVm group:
                key = (group.Title ?? string.Empty).Trim();
                break;

            case string keyText:
                key = keyText.Trim();
                break;
        }

        var nav = ResolveNavigation();
        if (key.Length == 0 || nav == null)
            return;

        var lookup = await SpecialisationService.GetAllAsync();
        var match = lookup.FirstOrDefault(kvp => string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(match.Key) || match.Value == null)
            return;

        await nav.PushAsync(new SpecialisationCardPage(match.Key, match.Value, selectedOption));
    }

    private async void ViewSpecialisationRowDetails(object? parameter)
    {
        if (parameter is not CharacterSpecialisationVm.SpecialisationAbilityRow row)
            return;

        var key = (row.SpecialisationKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return;

        var selectedOption = (row.SelectedAbility ?? row.Ability ?? row.SelectedOption ?? string.Empty).Trim();
        if (selectedOption.Length == 0)
            selectedOption = (row.SelectedOption ?? string.Empty).Trim();

        var nav = ResolveNavigation();
        if (nav == null)
            return;

        var lookup = await SpecialisationService.GetAllAsync();
        var match = lookup.FirstOrDefault(kvp => string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(match.Key) || match.Value == null)
            return;

        await nav.PushAsync(new SpecialisationCardPage(match.Key, match.Value, selectedOption));
    }

    private INavigation? ResolveNavigation()
    {
        if (Navigation?.NavigationStack is { Count: > 0 })
            return Navigation;

        if (Shell.Current?.Navigation is { } shellNav)
            return shellNav;

        return Application.Current?.MainPage?.Navigation;
    }
}
