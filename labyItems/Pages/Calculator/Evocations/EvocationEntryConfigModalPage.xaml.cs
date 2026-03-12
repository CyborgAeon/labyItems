using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator;

public partial class EvocationEntryConfigModalPage : ContentPage
{
    private readonly EvocationSelectionEntry _target;
    private readonly EvocationSelectionEntry _draft;

    public EvocationEntryConfigModalPage(EvocationSelectionEntry target)
    {
        InitializeComponent();
        _target = target;
        _draft = target.Clone();
        _draft.NormalizeForEvocationType();
        BindingContext = _draft;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _draft.NormalizeForEvocationType();
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
