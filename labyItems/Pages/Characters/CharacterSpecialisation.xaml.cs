using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Characters;

public partial class CharacterSpecialisation : ContentView
{
    private CharacterSpecialisationVm? _boundVm;

    public CharacterSpecialisation()
    {
        InitializeComponent();
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

    private async void OnViewSpecialisationAbilityDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.CommandParameter is not SpecialisationSlotVm slot)
            return;

        var abilityName = slot.SelectedAbilityNameForDetails;
        if (string.IsNullOrWhiteSpace(abilityName))
            return;

        var ability = await AbilityDetailsLookupService.FindByIndexAsync(abilityName);
        if (ability == null || Navigation == null)
            return;

        await Navigation.PushAsync(new AbilityCardPage(ability));
    }
}
