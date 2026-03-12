using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator;

public partial class GeneralAbilityImmunityModalPage : ContentPage
{
    private readonly GeneralAbilitySelectionEntry _target;
    private readonly GeneralAbilitySelectionEntry _draft;

    public GeneralAbilityImmunityModalPage(GeneralAbilitySelectionEntry target)
    {
        InitializeComponent();
        _target = target;
        _draft = new GeneralAbilitySelectionEntry(target.Ability)
        {
            ImmunityOnlyFirstTimeNeeded = target.ImmunityOnlyFirstTimeNeeded
        };
        BindingContext = _draft;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _target.ImmunityOnlyFirstTimeNeeded = _draft.ImmunityOnlyFirstTimeNeeded;
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (Navigation.ModalStack.Count > 0)
        {
            await Navigation.PopModalAsync();
            return;
        }

        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}
