using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardLegacyCreatePage : ContentPage
{
    private readonly NonStandardCreateVm _vm = new();
    private bool _appeared;

    public NonStandardLegacyCreatePage()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    public async Task LoadFromWalletEntryAsync(NonStandardWalletEntry entry)
    {
        try
        {
            await _vm.LoadFromWalletEntryAsync(entry);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Load failed", ex.Message, "OK");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_appeared)
            return;

        _appeared = true;
        try
        {
            await _vm.InitializeAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Load failed", ex.Message, "OK");
        }
    }

    private async void OnSearchBaseClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SearchBaseAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private async void OnSearchFieldClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        var field = button.CommandParameter as NonStandardFieldVm
            ?? button.BindingContext as NonStandardFieldVm;
        if (field == null)
            return;

        try
        {
            await _vm.SearchFieldAsync(Navigation, field);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SaveAsync();
            if (_vm.HasSaveStatus)
                await DisplayAlert("Saved", _vm.SaveStatus, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    private void OnToggleLifeScaleClicked(object sender, EventArgs e)
    {
        _vm.ToggleLifeScaleExpanded();
    }
}
