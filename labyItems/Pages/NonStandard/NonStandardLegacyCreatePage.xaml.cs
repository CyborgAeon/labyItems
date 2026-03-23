using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardLegacyCreatePage : ContentPage
{
    private readonly NonStandardCreateVm _vm = new();
    private bool _appeared;

    public string? FixedEntityTypeKey { get; set; }

    public NonStandardLegacyCreatePage()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    public async Task LoadFromWalletEntryAsync(NonStandardWalletEntry entry)
    {
        try
        {
            var fixedType = ResolveFixedEntityType();
            if (fixedType.HasValue && entry.EntityType != fixedType.Value)
                throw new InvalidOperationException($"This tab only edits {fixedType.Value} entries.");

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
        var fixedType = ResolveFixedEntityType();
        TypeSelectorSection.IsVisible = !fixedType.HasValue;

        try
        {
            if (!_appeared)
            {
                _appeared = true;
                await _vm.InitializeAsync(fixedType);
            }
            else
            {
                await _vm.RefreshLookupsOnAppearAsync();
            }
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

    private async void OnSearchRaceAbilityClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SearchRaceAbilityAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private async void OnEditRaceAbilityClicked(object sender, EventArgs e)
    {
        var row = (sender as Button)?.CommandParameter as RaceAbilityRowVm
            ?? (sender as BindableObject)?.BindingContext as RaceAbilityRowVm;
        if (row == null)
            return;

        try
        {
            await Navigation.PushModalAsync(new NonStandardRaceAbilityRowEditorPage(_vm, row));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Edit failed", ex.Message, "OK");
        }
    }

    private async void OnRaceAbilityInfoClicked(object sender, EventArgs e)
    {
        var row = (sender as Button)?.CommandParameter as RaceAbilityRowVm
            ?? (sender as BindableObject)?.BindingContext as RaceAbilityRowVm;
        if (row == null)
            return;

        var info = _vm.BuildRaceAbilityInfoText(row);
        await DisplayAlert("Ability info", info, "OK");
    }

    private void OnDeleteRaceAbilityClicked(object sender, EventArgs e)
    {
        var row = (sender as Button)?.CommandParameter as RaceAbilityRowVm
            ?? (sender as BindableObject)?.BindingContext as RaceAbilityRowVm;
        _vm.RemoveRaceAbilityRow(row);
    }

    private void OnAddSubtypeCopyClicked(object sender, EventArgs e)
    {
        var option = (sender as Button)?.CommandParameter as RaceSubtypeOptionVm
            ?? (sender as BindableObject)?.BindingContext as RaceSubtypeOptionVm;
        _vm.AddSubtypeCopyFromOption(option);
    }

    private void OnAddBlankSubtypeCopyClicked(object sender, EventArgs e)
        => _vm.AddBlankSubtypeCopy();

    private void OnDeleteSubtypeCopyClicked(object sender, EventArgs e)
    {
        var copy = (sender as Button)?.CommandParameter as RaceSubtypeCopyVm
            ?? (sender as BindableObject)?.BindingContext as RaceSubtypeCopyVm;
        _vm.RemoveSubtypeCopy(copy);
    }

    private async void OnSearchSubtypeCopyAbilityClicked(object sender, EventArgs e)
    {
        var copy = (sender as Button)?.CommandParameter as RaceSubtypeCopyVm
            ?? (sender as BindableObject)?.BindingContext as RaceSubtypeCopyVm;
        if (copy == null)
            return;

        try
        {
            await _vm.SearchSubtypeCopyAbilityAsync(Navigation, copy);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnToggleRaceAlignmentClicked(object sender, EventArgs e)
        => _vm.ToggleRaceAlignmentExpanded();

    private void OnToggleLifeScaleClicked(object sender, EventArgs e)
    {
        _vm.ToggleLifeScaleExpanded();
    }

    private NonStandardEntityType? ResolveFixedEntityType()
    {
        var raw = (FixedEntityTypeKey ?? string.Empty).Trim();
        if (raw.Length == 0)
            return null;

        return Enum.TryParse<NonStandardEntityType>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }
}
