using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardClassCreatePage : ContentPage
{
    private readonly NonStandardClassCreateVm _vm = new();
    private bool _appeared;

    public NonStandardClassCreatePage()
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

    private async void OnSearchBaseClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SearchBaseClassAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private async void OnSearchPathClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SearchPathClassAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnAddArmourRestrictionClicked(object sender, EventArgs e)
        => _vm.AddArmourRestriction();

    private void OnDeleteArmourRestrictionClicked(object sender, EventArgs e)
        => _vm.RemoveArmourRestriction((sender as Button)?.CommandParameter?.ToString());

    private void OnAddWeaponSkillRestrictionClicked(object sender, EventArgs e)
        => _vm.AddWeaponSkillRestriction();

    private void OnDeleteWeaponSkillRestrictionClicked(object sender, EventArgs e)
        => _vm.RemoveWeaponSkillRestriction((sender as Button)?.CommandParameter?.ToString());

    private async void OnEditWeaponSkillLevelClicked(object sender, EventArgs e)
    {
        var row = (sender as Button)?.CommandParameter as WeaponSkillLevelVm
            ?? (sender as BindableObject)?.BindingContext as WeaponSkillLevelVm;
        if (row == null)
            return;

        try
        {
            await Navigation.PushModalAsync(new NonStandardWeaponSkillsLevelEditorPage(_vm, row));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Edit failed", ex.Message, "OK");
        }
    }

    private async void OnEditAbilityLevelClicked(object sender, EventArgs e)
    {
        var row = (sender as Button)?.CommandParameter as AbilityLevelVm
            ?? (sender as BindableObject)?.BindingContext as AbilityLevelVm;
        if (row == null)
            return;

        try
        {
            await Navigation.PushModalAsync(new NonStandardAbilityLevelEditorPage(_vm, row));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Edit failed", ex.Message, "OK");
        }
    }

    private void OnAddCasterColourClicked(object sender, EventArgs e)
        => _vm.AddCasterColourRow();

    private void OnDeleteCasterColourClicked(object sender, EventArgs e)
    {
        var row = (sender as Button)?.CommandParameter as CasterLevelRowVm
            ?? (sender as BindableObject)?.BindingContext as CasterLevelRowVm;
        _vm.RemoveCasterColourRow(row);
    }

    private async void OnSearchLifescaleClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SearchLifeScaleAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnToggleLifescaleExpandedClicked(object sender, EventArgs e)
        => _vm.ToggleLifeScaleExpanded();

    private async void OnSearchLifescaleRaceClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SearchLifeScaleRaceAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnAddLifescaleAssignmentClicked(object sender, EventArgs e)
        => _vm.AddLifeScaleAssignment();

    private void OnDeleteLifescaleAssignmentClicked(object sender, EventArgs e)
        => _vm.RemoveLifeScaleAssignment((sender as Button)?.CommandParameter?.ToString());

    private async void OnAddWhitelistRaceClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.AddRaceToWhitelistAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnMoveWhitelistToBlacklistClicked(object sender, EventArgs e)
        => _vm.MoveWhitelistToBlacklist((sender as Button)?.CommandParameter?.ToString());

    private void OnDeleteWhitelistRaceClicked(object sender, EventArgs e)
        => _vm.RemoveFromWhitelist((sender as Button)?.CommandParameter?.ToString());

    private async void OnAddBlacklistRaceClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.AddRaceToBlacklistAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnMoveBlacklistToWhitelistClicked(object sender, EventArgs e)
        => _vm.MoveBlacklistToWhitelist((sender as Button)?.CommandParameter?.ToString());

    private void OnDeleteBlacklistRaceClicked(object sender, EventArgs e)
        => _vm.RemoveFromBlacklist((sender as Button)?.CommandParameter?.ToString());

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
}
