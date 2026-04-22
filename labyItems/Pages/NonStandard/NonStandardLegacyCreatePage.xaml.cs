using labyItems.Services;
using labyItems.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardLegacyCreatePage : ContentPage
{
    private readonly NonStandardCreateVm _vm = new();
    private bool _appeared;
    private int _currentStepIndex;
    private readonly List<string> _stepKeys = new();

    public string? FixedEntityTypeKey { get; set; }

    public ObservableCollection<StepItem> StepItems { get; } = new();
    public Command<int> StepClickCommand { get; }

    public NonStandardLegacyCreatePage()
    {
        InitializeComponent();
        BindingContext = _vm;
        StepClickCommand = new Command<int>(SetStep);
        OnPropertyChanged(nameof(StepClickCommand));
        _vm.PropertyChanged += OnVmPropertyChanged;
        RebuildStepFlow(resetToStart: true);
    }

    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        private set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(0, StepItems.Count - 1));
            if (_currentStepIndex == clamped)
                return;

            _currentStepIndex = clamped;
            RaiseStepState();
        }
    }

    private string CurrentStepKey
    {
        get
        {
            if (_stepKeys.Count == 0)
                return "review";

            var index = Math.Clamp(CurrentStepIndex, 0, _stepKeys.Count - 1);
            return _stepKeys[index];
        }
    }

    private bool IsRaceFlow => _vm.IsRaceType;
    private bool IsAssignmentFlow => !IsRaceFlow && _vm.RequiresCharacterAssignment;
    private bool HasLifeScaleFlow => _vm.RequiresLifeScale;

    public bool ShowBackButton => StepItems.Count > 0;
    public int MaxAccessibleStep => Math.Max(0, CurrentStepIndex);
    public bool CanAdvance => !(_vm.IsBusy);
    public string NextButtonText => IsReviewStep ? "Save" : (CurrentStepIndex == StepItems.Count - 2 ? "Review" : "Next");
    public string StepCounterText => StepItems.Count == 0 ? string.Empty : $"Step {CurrentStepIndex + 1}/{StepItems.Count}";

    public bool ShowAssignmentSection => CurrentStepKey == "assign";
    public bool ShowBasicSection => CurrentStepKey == "base";
    public bool ShowRaceDetailsSection => CurrentStepKey == "race";
    public bool ShowRaceAbilitiesSection => CurrentStepKey == "abilities";
    public bool ShowFieldsSection => CurrentStepKey == "fields" || (IsRaceFlow && CurrentStepKey == "race");
    public bool ShowLifeScaleSection => CurrentStepKey == "lifescale";
    public bool ShowReviewSection => CurrentStepKey == "review";
    public bool ShowRaceReviewCards => IsRaceFlow && ShowReviewSection;
    public string ReviewBaseSummary => BuildBaseSummary();
    public string ReviewRaceDetailsSummary => BuildRaceDetailsSummary();
    public string ReviewAbilitiesSummary => BuildAbilitiesSummary();
    public string ReviewLifeScaleSummary => BuildLifeScaleSummary();

    private bool IsReviewStep => CurrentStepKey == "review";

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
                RebuildStepFlow(resetToStart: true);
            }
            else
            {
                await _vm.RefreshLookupsOnAppearAsync();
                RebuildStepFlow(resetToStart: false);
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

    private async void OnNextStepClicked(object sender, EventArgs e)
    {
        if (IsReviewStep)
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

            return;
        }

        SetStep(CurrentStepIndex + 1);
    }

    private void OnPreviousStepClicked(object sender, EventArgs e)
    {
        if (CurrentStepIndex == 0)
        {
            _ = NavigateBackToDashboardAsync();
            return;
        }

        SetStep(CurrentStepIndex - 1);
    }

    private void SetStep(int stepIndex)
    {
        var max = Math.Max(0, StepItems.Count - 1);
        CurrentStepIndex = Math.Clamp(stepIndex, 0, max);
        RaiseStepState();
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

    private async void OnSearchRaceTagsClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SearchRaceTagsAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnAddRaceTagClicked(object sender, EventArgs e)
        => _vm.AddRaceTag();

    private void OnRemoveRaceTagClicked(object sender, EventArgs e)
    {
        var tag = (sender as Button)?.CommandParameter as string
            ?? (sender as BindableObject)?.BindingContext as string;
        _vm.RemoveRaceTag(tag);
    }

    private async void OnSearchLifeScaleTargetClassClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.SearchLifeScaleTargetClassAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private async void OnAddRaceLifeScaleEntryClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.AddRaceLifeScaleEntryAsync(Navigation);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private async void OnSearchRaceLifeScaleClassClicked(object sender, EventArgs e)
    {
        try
        {
            var entry = (sender as Button)?.CommandParameter as RaceLifeScaleClassEntryVm;
            await _vm.SearchRaceLifeScaleClassAsync(Navigation, entry);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnToggleRaceLifeScaleEditorClicked(object sender, EventArgs e)
    {
        var entry = (sender as Button)?.CommandParameter as RaceLifeScaleClassEntryVm;
        _vm.ToggleRaceLifeScaleEntryEditor(entry);
    }

    private void OnRemoveRaceLifeScaleEntryClicked(object sender, EventArgs e)
    {
        var entry = (sender as Button)?.CommandParameter as RaceLifeScaleClassEntryVm;
        _vm.RemoveRaceLifeScaleEntry(entry);
    }

    private void OnReviewEditBaseClicked(object sender, EventArgs e)
        => SetStepForKey("base");

    private void OnReviewEditRaceDetailsClicked(object sender, EventArgs e)
        => SetStepForKey("race");

    private void OnReviewEditAbilitiesClicked(object sender, EventArgs e)
        => SetStepForKey("abilities");

    private void OnReviewEditLifeScaleClicked(object sender, EventArgs e)
        => SetStepForKey("lifescale");

    private NonStandardEntityType? ResolveFixedEntityType()
    {
        var raw = (FixedEntityTypeKey ?? string.Empty).Trim();
        if (raw.Length == 0)
            return null;

        return Enum.TryParse<NonStandardEntityType>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NonStandardCreateVm.SelectedEntityType)
            or nameof(NonStandardCreateVm.RequiresCharacterAssignment)
            or nameof(NonStandardCreateVm.IsRaceType)
            or nameof(NonStandardCreateVm.RequiresLifeScale))
        {
            MainThread.BeginInvokeOnMainThread(() => RebuildStepFlow(resetToStart: true));
            return;
        }

        MainThread.BeginInvokeOnMainThread(RaiseReviewState);
    }

    private void RebuildStepFlow(bool resetToStart)
    {
        _stepKeys.Clear();
        StepItems.Clear();

        if (IsRaceFlow)
        {
            AddStep("base", "Base");
            AddStep("race", "Details");
            AddStep("abilities", "Abilities");
            if (HasLifeScaleFlow)
                AddStep("lifescale", "Life");
            AddStep("review", "Review");
        }
        else
        {
            if (IsAssignmentFlow)
                AddStep("assign", "Assign");
            AddStep("base", "Base");
            AddStep("fields", "Fields");
            AddStep("review", "Review");
        }

        if (resetToStart)
            _currentStepIndex = 0;
        else
            _currentStepIndex = Math.Clamp(_currentStepIndex, 0, Math.Max(0, StepItems.Count - 1));

        RaiseStepState();
    }

    private void AddStep(string key, string label)
    {
        var index = StepItems.Count;
        _stepKeys.Add(key);
        StepItems.Add(new StepItem { Id = index, Label = label });
    }

    private void RaiseStepState()
    {
        OnPropertyChanged(nameof(CurrentStepIndex));
        OnPropertyChanged(nameof(ShowBackButton));
        OnPropertyChanged(nameof(MaxAccessibleStep));
        OnPropertyChanged(nameof(CanAdvance));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(StepCounterText));
        OnPropertyChanged(nameof(ShowAssignmentSection));
        OnPropertyChanged(nameof(ShowBasicSection));
        OnPropertyChanged(nameof(ShowRaceDetailsSection));
        OnPropertyChanged(nameof(ShowRaceAbilitiesSection));
        OnPropertyChanged(nameof(ShowFieldsSection));
        OnPropertyChanged(nameof(ShowLifeScaleSection));
        OnPropertyChanged(nameof(ShowReviewSection));
        OnPropertyChanged(nameof(ShowRaceReviewCards));
        RaiseReviewState();
        WizardStepIndicator.CurrentStep = CurrentStepIndex;
    }

    private void RaiseReviewState()
    {
        OnPropertyChanged(nameof(ReviewBaseSummary));
        OnPropertyChanged(nameof(ReviewRaceDetailsSummary));
        OnPropertyChanged(nameof(ReviewAbilitiesSummary));
        OnPropertyChanged(nameof(ReviewLifeScaleSummary));
    }

    private void SetStepForKey(string stepKey)
    {
        var index = _stepKeys.FindIndex(key => string.Equals(key, stepKey, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            SetStep(index);
    }

    private string BuildBaseSummary()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_vm.Name))
            parts.Add($"Name: {_vm.Name.Trim()}");
        if (!string.IsNullOrWhiteSpace(_vm.SelectedBaseTemplate?.Name))
            parts.Add($"Base: {_vm.SelectedBaseTemplate.Name}");
        if (!string.IsNullOrWhiteSpace(_vm.RaceBuyAsText))
            parts.Add($"Buy as: {_vm.RaceBuyAsText.Trim()}");
        if (!string.IsNullOrWhiteSpace(_vm.RaceSubtypeDisplayName))
            parts.Add($"Subtype: {_vm.RaceSubtypeDisplayName.Trim()}");
        else if (!string.IsNullOrWhiteSpace(_vm.RaceSubtypeKey))
            parts.Add($"Subtype: {_vm.RaceSubtypeKey.Trim()}");
        if (_vm.SelectedRaceTags.Count > 0)
            parts.Add($"Tags: {string.Join(", ", _vm.SelectedRaceTags)}");
        return parts.Count == 0 ? "No base details configured yet." : string.Join(Environment.NewLine, parts);
    }

    private string BuildRaceDetailsSummary()
    {
        var parts = new List<string>();
        if (_vm.SelectedRacePeopleTypes.Count > 0)
            parts.Add($"People type: {string.Join(", ", _vm.SelectedRacePeopleTypes)}");

        var description = GetFieldValue("Description");
        if (!string.IsNullOrWhiteSpace(description))
            parts.Add($"Description: {description}");

        var additionalInfo = GetFieldValue("AdditionalInfo");
        if (!string.IsNullOrWhiteSpace(additionalInfo))
            parts.Add($"Additional info: {additionalInfo}");

        if (_vm.SelectedRaceGuildOverrideTypes.Count > 0)
            parts.Add($"Guild overrides: {string.Join(", ", _vm.SelectedRaceGuildOverrideTypes)}");

        if (_vm.SelectedRaceAlignments.Count > 0)
            parts.Add($"Alignment rule: {string.Join(", ", _vm.SelectedRaceAlignments)}");

        return parts.Count == 0 ? "No race details configured yet." : string.Join(Environment.NewLine, parts);
    }

    private string BuildAbilitiesSummary()
    {
        if (_vm.RaceAbilityRows.Count == 0)
            return "No levelled abilities added yet.";

        return string.Join(Environment.NewLine, _vm.RaceAbilityRows.Select(row => row.RowSummary));
    }

    private string BuildLifeScaleSummary()
    {
        if (_vm.ShowRaceLifeScaleSelectors)
        {
            var entries = _vm.RaceLifeScaleEntries;
            if (entries.Count == 0)
                return "No class lifescale assignments yet.";

            return string.Join(Environment.NewLine,
                entries.Select(entry => $"Class: {entry.ClassNameDisplay}"));
        }

        var parts = new List<string>();
        parts.Add(_vm.LifeScaleCollapsedSummary);
        return string.Join(Environment.NewLine, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string GetFieldValue(string label)
    {
        var field = _vm.Fields.FirstOrDefault(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase));
        return (field?.Value ?? string.Empty).Trim();
    }

    private async Task NavigateBackToDashboardAsync()
    {
        if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
            return;
        }

        await Shell.Current.GoToAsync("..");
    }
}
