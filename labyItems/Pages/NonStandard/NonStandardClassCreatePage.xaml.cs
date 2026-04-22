using labyItems.Services;
using labyItems.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardClassCreatePage : ContentPage
{
    private readonly NonStandardClassCreateVm _vm = new();
    private bool _appeared;
    private int _currentStepIndex;

    public ObservableCollection<StepItem> StepItems { get; } =
    [
        new StepItem { Id = 0, Label = "Base" },
        new StepItem { Id = 1, Label = "Life" },
        new StepItem { Id = 2, Label = "Armour" },
        new StepItem { Id = 3, Label = "Skills" },
        new StepItem { Id = 4, Label = "Power" },
        new StepItem { Id = 5, Label = "Review" }
    ];

    public Command<int> StepClickCommand { get; }
    public Command<string> AddRaceAssignmentCommand { get; }

    public NonStandardClassCreatePage()
    {
        StepClickCommand = new Command<int>(OnStepBubbleClicked);
        AddRaceAssignmentCommand = new Command<string>(OnRaceAssignmentSelected);
        InitializeComponent();
        BindingContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.LifeScaleAssignments.CollectionChanged += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(MaxAccessibleStep));
                OnPropertyChanged(nameof(ReviewLifeSummary));
            });
        };
    }

    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        private set
        {
            if (_currentStepIndex == value)
                return;

            _currentStepIndex = Math.Clamp(value, 0, StepItems.Count - 1);
            OnPropertyChanged(nameof(CurrentStepIndex));
            OnPropertyChanged(nameof(IsBaseStep));
            OnPropertyChanged(nameof(IsLifeStep));
            OnPropertyChanged(nameof(IsArmourStep));
            OnPropertyChanged(nameof(IsSkillsStep));
            OnPropertyChanged(nameof(IsPowerStep));
            OnPropertyChanged(nameof(IsReviewStep));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(NextButtonText));
            OnPropertyChanged(nameof(StepCounterText));
        }
    }

    public bool IsBaseStep => CurrentStepIndex == 0;
    public bool IsLifeStep => CurrentStepIndex == 1;
    public bool IsArmourStep => CurrentStepIndex == 2;
    public bool IsSkillsStep => CurrentStepIndex == 3;
    public bool IsPowerStep => CurrentStepIndex == 4;
    public bool IsReviewStep => CurrentStepIndex == 5;
    public bool CanGoPrevious => !_vm.IsBusy;
    public bool CanGoNext => ResolveCanGoNext();
    public string NextButtonText => IsReviewStep ? "Finish" : "Next";
    public string StepCounterText => $"Step {CurrentStepIndex + 1}/{StepItems.Count}";
    public int MaxAccessibleStep => ResolveMaxAccessibleStep();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_appeared)
            return;

        _appeared = true;
        try
        {
            await _vm.InitializeAsync();
            OnPropertyChanged(nameof(ReviewBaseSummary));
            OnPropertyChanged(nameof(ReviewLifeSummary));
            OnPropertyChanged(nameof(ReviewArmourSummary));
            OnPropertyChanged(nameof(ReviewSkillsSummary));
            OnPropertyChanged(nameof(ReviewPowerSummary));
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

    private async void OnSearchBuyAsClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SearchBuyAsAsync(Navigation);
            OnPropertyChanged(nameof(ReviewBaseSummary));
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

    private void OnUseLifeScaleOptionClicked(object sender, EventArgs e)
    {
        var option = (sender as Button)?.CommandParameter as LifeScaleSearchOptionVm
            ?? (sender as BindableObject)?.BindingContext as LifeScaleSearchOptionVm;
        _vm.SelectLifeScaleSearchOption(option);
        OnPropertyChanged(nameof(ReviewLifeSummary));
    }

    private void OnToggleLifeScaleAdvancedClicked(object sender, EventArgs e)
        => _vm.ToggleLifeScaleAdvancedExpanded();

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

    private void OnNextStepClicked(object sender, EventArgs e)
    {
        if (IsReviewStep)
        {
            _ = SaveAndExitAsync();
            return;
        }

        SetStep(CurrentStepIndex + 1);
    }

    private async Task SaveAndExitAsync()
    {
        if (!_vm.CanSave)
        {
            await DisplayAlert("Incomplete", "Complete required sections before saving.", "OK");
            return;
        }

        try
        {
            await _vm.SaveAsync();
            await Navigation.PushAsync(new NonStandardWalletPage());
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    private async void OnPreviousStepClicked(object sender, EventArgs e)
    {
        if (CurrentStepIndex == 0)
        {
            if (Navigation.NavigationStack.LastOrDefault() == this)
                await Navigation.PopAsync();
            else if (Navigation.ModalStack.LastOrDefault() == this)
                await Navigation.PopModalAsync();

            return;
        }

        SetStep(CurrentStepIndex - 1);
    }

    private void SetStep(int index)
    {
        CurrentStepIndex = index;
        OnPropertyChanged(nameof(ReviewBaseSummary));
        OnPropertyChanged(nameof(ReviewLifeSummary));
        OnPropertyChanged(nameof(ReviewArmourSummary));
        OnPropertyChanged(nameof(ReviewSkillsSummary));
        OnPropertyChanged(nameof(ReviewPostEighthSummary));
        OnPropertyChanged(nameof(ReviewPowerSummary));
        OnPropertyChanged(nameof(ReviewSaveHint));
    }

    private void OnStepBubbleClicked(int index)
    {
        if (!CanNavigateToStep(index))
            return;

        SetStep(index);
    }

    private bool CanNavigateToStep(int index)
    {
        var clampedIndex = Math.Clamp(index, 0, StepItems.Count - 1);
        if (clampedIndex <= CurrentStepIndex)
            return true;

        return clampedIndex <= ResolveMaxAccessibleStep();
    }

    private int ResolveMaxAccessibleStep()
    {
        if (!_vm.HasBaseSelection)
            return 0;

        if (_vm.LifeScaleAssignments.Count == 0)
            return 1;

        return 5;
    }

    private bool ResolveCanGoNext()
    {
        if (_vm.IsBusy)
            return false;

        if (IsBaseStep)
            return _vm.HasBaseSelection;

        if (IsLifeStep)
            return _vm.LifeScaleAssignments.Count > 0;

        if (IsReviewStep)
            return _vm.CanSave;

        return true;
    }

    private void OnRaceAssignmentSelected(string? raceName)
    {
        _vm.AddLifeScaleAssignment(raceName);
        OnPropertyChanged(nameof(ReviewLifeSummary));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(MaxAccessibleStep));
    }

    private void OnToggleLifeScaleSearchClicked(object sender, EventArgs e)
        => _vm.ToggleLifeScaleSearchExpanded();

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NonStandardClassCreateVm.CanSave)
            or nameof(NonStandardClassCreateVm.IsBusy)
            or nameof(NonStandardClassCreateVm.HasBaseSelection)
            or nameof(NonStandardClassCreateVm.LifeScaleAssignments))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(NextButtonText));
                OnPropertyChanged(nameof(MaxAccessibleStep));
            });
        }
    }

    public string ReviewBaseSummary
        => $"Name: {FormatValue(_vm.Name)}\nBase class: {FormatValue(_vm.BaseClassName)}\nPath class: {FormatValue(_vm.PathClassName)}\nBuy-as: {FormatValue(_vm.BuyAsText)}\nBrackets: {FormatList(_vm.SelectedBrackets)}";

    public string ReviewLifeSummary
        => $"Selection: {FormatValue(_vm.LifescaleSummary)}\nAssignments: {(_vm.LifeScaleAssignments.Count == 0 ? "None yet" : string.Join("; ", _vm.LifeScaleAssignments.Select(item => item.RaceClassSummary)))}";

    public string ReviewArmourSummary
        => $"Allowed armour: {FormatList(_vm.SelectedArmourOptions)}\nRestrictions: {FormatList(_vm.ArmourRestrictions)}";

    public string ReviewSkillsSummary
        => $"Weapon rows: {_vm.WeaponSkillLevels.Count}\nWeapon restrictions: {FormatList(_vm.WeaponSkillRestrictions)}\nAbility rows: {_vm.AbilityLevels.Sum(level => level.Abilities.Count)}";

    public string ReviewPostEighthSummary
        => _vm.PostEighthEntries.Count == 0
            ? "No multi-race or multi-class progression configured."
            : string.Join("\n", _vm.PostEighthEntries.Select(entry => entry.ReviewSummary));

    public string ReviewPowerSummary
        => $"Powerbases: {FormatList(_vm.SelectedPowerbases)}\nRebirth: {(_vm.IsRebirth ? "Yes" : "No")}\nAllowed alignments: {_vm.SelectedAlignments.Count}";

    public string ReviewSaveHint
        => _vm.CanSave
            ? "Review looks complete. Save will write the non-standard class and its lifescale assignments."
            : "Save stays disabled until the class has a name, at least one bracket, and at least one race lifescale assignment.";

    private void OnEditBaseStepClicked(object sender, EventArgs e) => SetStep(0);
    private void OnEditLifeStepClicked(object sender, EventArgs e) => SetStep(1);
    private void OnEditArmourStepClicked(object sender, EventArgs e) => SetStep(2);
    private void OnEditSkillsStepClicked(object sender, EventArgs e) => SetStep(3);
    private void OnEditPowerStepClicked(object sender, EventArgs e) => SetStep(4);

    private void OnTogglePostEighthClicked(object sender, EventArgs e)
        => _vm.TogglePostEighthExpanded();

    private async void OnAddPostEighthRaceClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.AddPostEighthRaceAsync(Navigation);
            OnPropertyChanged(nameof(ReviewSkillsSummary));
            OnPropertyChanged(nameof(ReviewPostEighthSummary));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private async void OnAddPostEighthClassClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.AddPostEighthClassAsync(Navigation);
            OnPropertyChanged(nameof(ReviewSkillsSummary));
            OnPropertyChanged(nameof(ReviewPostEighthSummary));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnDeletePostEighthEntryClicked(object sender, EventArgs e)
    {
        var entry = (sender as Button)?.CommandParameter as PostEighthEntryVm
            ?? (sender as BindableObject)?.BindingContext as PostEighthEntryVm;
        _vm.RemovePostEighthEntry(entry);
        OnPropertyChanged(nameof(ReviewPostEighthSummary));
    }

    private static string FormatList(IEnumerable<string> values)
    {
        var items = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();

        return items.Count == 0 ? "None" : string.Join(", ", items);
    }

    private static string FormatValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Not set" : value.Trim();
}
