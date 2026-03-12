namespace labyItems.Pages.Configs;

public partial class MiracleEntryConfigModalPage : ContentPage
{
    private readonly MiracleSelectionEntry _target;
    private readonly MiracleSelectionEntry _draft;

    public MiracleEntryConfigModalPage(MiracleSelectionEntry target)
    {
        InitializeComponent();
        _target = target;
        _draft = target.Clone();
        _draft.NormalizeForMiracleType();
        BindingContext = _draft;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _draft.NormalizeForMiracleType();
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
