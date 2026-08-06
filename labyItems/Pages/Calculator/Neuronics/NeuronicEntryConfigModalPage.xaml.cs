using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator;

public partial class NeuronicEntryConfigModalPage : ContentPage
{
    private readonly NeuronicSelectionEntry _target;
    private readonly NeuronicSelectionEntry _draft;

    public NeuronicEntryConfigModalPage(NeuronicSelectionEntry target)
    {
        InitializeComponent();
        _target = target;
        _draft = target.Clone();
        BindingContext = _draft;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
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
