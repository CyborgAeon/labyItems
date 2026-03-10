namespace labyItems.Pages.NonStandard;

public partial class NonStandardCreatePage : ContentPage
{
    private readonly NonStandardCreateVm _vm = new();
    private bool _appeared;

    public NonStandardCreatePage()
    {
        InitializeComponent();
        BindingContext = _vm;
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
