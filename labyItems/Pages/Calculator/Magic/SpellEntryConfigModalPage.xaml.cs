using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator;

public partial class SpellEntryConfigModalPage : ContentPage
{
    private readonly SpellSelectionEntry _target;
    private readonly SpellSelectionEntry _draft;

    public SpellEntryConfigModalPage(SpellSelectionEntry target)
    {
        InitializeComponent();
        _target = target;
        _draft = target.Clone();
        _draft.NormalizeForSpellType();
        BindingContext = _draft;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _draft.NormalizeForSpellType();
        _target.ApplyFrom(_draft);
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
