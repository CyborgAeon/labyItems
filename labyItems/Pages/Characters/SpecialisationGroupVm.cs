
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using labyItems.Controls;
using labyItems.Controls.Pickers;
using labyItems.Models.Characters;
using labyItems.Models.Enums;

namespace labyItems.Pages.Characters;

public sealed record SpecialisationGroupConfig(
    string Title,
    IEnumerable<int> Levels,
    IOptionSource OptionSource,
    Action OnAnySelectionChanged,
    Func<IReadOnlyList<string>, string?>? SelectionValidator = null,
    bool IsOptional = false,
    Dictionary<string, AbilityCustomisation>? OptionCustomisations = null,
    Func<AbilityCustomisation?, Dictionary<string, string>?>? CustomisationOptionsProvider = null,
    bool HideAbilityPickerWhenSingleOption = true);

public sealed class SpecialisationGroupVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private readonly Action _onAnySelectionChanged;
    private readonly bool _useMagicColourEnum;
    private readonly bool _useVivomancerColourEnum;
    private readonly Func<IReadOnlyList<string>, string?>? _selectionValidator;
    private readonly Dictionary<string, AbilityCustomisation> _optionCustomisations;
    private readonly Func<AbilityCustomisation?, Dictionary<string, string>?>? _customisationOptionsProvider;
    private readonly IOptionSource _optionSource;
    private bool _isOptional;
    private bool _isVisible = true;

    public string Title { get; }
    public int RequiredCount => Slots.Count;

    public ObservableCollection<SpecialisationSlotVm> Slots { get; } = new();

    private List<string> _allOptionNames;
    public IReadOnlyList<string> OptionNames => _allOptionNames;

    public bool IsOptional
    {
        get => _isOptional;
        set
        {
            if (_isOptional == value) return;
            _isOptional = value;
            RaiseComputed();
            Raise(nameof(Subtitle));
            Raise(nameof(StatusText));
            Raise(nameof(HelperText));
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public Command ToggleExpandedCommand { get; }

    public string Subtitle
    {
        get
        {
            if (_isOptional)
                return RequiredCount == 1 ? "Optional choice" : $"Pick up to {RequiredCount} abilities";

            return RequiredCount == 1 ? "Pick 1 ability" : $"Pick {RequiredCount} abilities";
        }
    }

    public int SelectedCount => Slots.Count(s => s.HasSelection);

    public bool HasDuplicates
    {
        get
        {
            var picked = Slots
                .Select(s => s.SelectionKey)
                .Where(x => x.Length > 0)
                .ToList();

            return picked.Count != picked.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }
    }

    private string _validationMessage = string.Empty;
    public string ValidationMessage
    {
        get => _validationMessage;
        private set => Set(ref _validationMessage, value);
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    private string _issueMessage = string.Empty;
    public string IssueMessage
    {
        get => _issueMessage;
        private set => Set(ref _issueMessage, value);
    }

    public bool HasIssue => !string.IsNullOrWhiteSpace(IssueMessage);

    public bool IsComplete
    {
        get
        {
            if (_isOptional)
                return !HasDuplicates && !HasValidationError && !HasIssue;

            return SelectedCount == RequiredCount && !HasDuplicates && !HasValidationError && !HasIssue;
        }
    }

    public string StatusText
    {
        get
        {
            if (_isOptional && SelectedCount == 0) return "Optional";
            return $"{SelectedCount}/{RequiredCount}";
        }
    }

    public string HelperText
    {
        get
        {
            if (HasValidationError) return ValidationMessage;
            if (HasDuplicates) return "Duplicate selections detected. Choose different abilities for each level.";
            if (HasIssue) return IssueMessage;
            if (_isOptional && SelectedCount == 0) return "Optional selection.";
            if (SelectedCount == 0) return "Make your selections below.";
            if (IsComplete) return "Selection complete.";
            return "Continue selecting until all levels are filled.";
        }
    }

    public SpecialisationCardState CardState
    {
        get
        {
            if (HasDuplicates || HasValidationError) return SpecialisationCardState.Error;
            if (HasIssue) return SpecialisationCardState.Issue;
            if (IsComplete) return SpecialisationCardState.Success;
            return SpecialisationCardState.Neutral;
        }
    }
    public enum SpecialisationCardState
    {
        Neutral,
        Success,
        Issue,
        Error
    }
    public bool UseWardPactEnum { get; }

    public SpecialisationGroupVm(
        SpecialisationGroupConfig cfg,
        Dictionary<int, string>? initiallySelectedByLevel = null)
    {
        Title = cfg.Title;
        _onAnySelectionChanged = cfg.OnAnySelectionChanged;
        _selectionValidator = cfg.SelectionValidator;
        _isOptional = cfg.IsOptional;
        _customisationOptionsProvider = cfg.CustomisationOptionsProvider;
        _optionCustomisations = cfg.OptionCustomisations != null
            ? new Dictionary<string, AbilityCustomisation>(cfg.OptionCustomisations, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AbilityCustomisation>(StringComparer.OrdinalIgnoreCase);

        _optionSource = cfg.OptionSource;
        _useMagicColourEnum = _optionSource.EnumType == typeof(MagicColours);
        _useVivomancerColourEnum = _optionSource.EnumType == typeof(VivomancerColours);
        UseWardPactEnum = _optionSource.Mode == SlotOptionMode.WardPactEnum;
        _allOptionNames = _optionSource.GetOptionNames().ToList();

        // Build slots
        foreach (var lvl in cfg.Levels.OrderBy(x => x))
        {
            initiallySelectedByLevel ??= new Dictionary<int, string>();
            initiallySelectedByLevel.TryGetValue(lvl, out var pre);

            var slot = new SpecialisationSlotVm(
                level: lvl,
                mode: _optionSource.Mode,
                enumType: _optionSource.EnumType,
                onChanged: OnSlotChanged,
                customisationResolver: ResolveCustomisation,
                customisationOptionsProvider: _customisationOptionsProvider,
                hideAbilityPickerWhenSingleOption: cfg.HideAbilityPickerWhenSingleOption);

            _optionSource.ApplyToSlot(slot, pre);
            Slots.Add(slot);
        }

        UpdateValidation();
        RaiseComputed();
    }
    public void RefreshCustomisationOptions()
    {
        foreach (var slot in Slots)
            slot.RefreshCustomisationOptions();
    }

    private AbilityCustomisation? ResolveCustomisation(string? option)
    {
        if (string.IsNullOrWhiteSpace(option))
            return null;

        return _optionCustomisations.TryGetValue(option.Trim(), out var custom) ? custom : null;
    }

    public void UpdateOptionNames(IEnumerable<string> optionNames)
    {
        _allOptionNames = optionNames?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        Raise(nameof(OptionNames));
        foreach (var s in Slots)
            s.RaiseFilteredOptionsChanged();
    }

    public void UpdateMagicColourOptions(IEnumerable<MagicColours> allowed)
    {
        if (!_useMagicColourEnum) return;

        var list = allowed?.ToList() ?? new List<MagicColours>();
        _allOptionNames = list
            .Select(EnumDisplayFormatter.Format)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Raise(nameof(OptionNames));

        foreach (var slot in Slots)
            slot.ConfigureMagicColours(list);

        UpdateValidation();
        RaiseComputed();
    }

    public void UpdateVivomancerColourOptions(IEnumerable<VivomancerColours> allowed)
    {
        if (!_useVivomancerColourEnum) return;

        var list = allowed?.ToList() ?? new List<VivomancerColours>();
        _allOptionNames = list
            .Select(EnumDisplayFormatter.Format)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Raise(nameof(OptionNames));

        foreach (var slot in Slots)
            slot.ConfigureVivomancerColours(list);

        UpdateValidation();
        RaiseComputed();
    }

    public void ApplyForcedSelections(Dictionary<int, AbilityDefinition> forcedByLevel, Func<AbilityDefinition, string>? displayFormatter = null)
    {
        bool changed = false;
        var map = forcedByLevel ?? new Dictionary<int, AbilityDefinition>();

        foreach (var slot in Slots)
        {
            if (map.TryGetValue(slot.Level, out var def) && def != null)
                changed |= slot.ApplyForcedAbility(def, displayFormatter?.Invoke(def), suppressNotify: true);
            else
                changed |= slot.ClearForcedAbility(suppressNotify: true);
        }

        if (!changed)
            return;

        UpdateValidation();
        RaiseComputed();
        _onAnySelectionChanged();
    }

    private void OnSlotChanged()
    {
        UpdateValidation();
        RaiseComputed();
        _onAnySelectionChanged();
    }

    private void RaiseComputed()
    {
        Raise(nameof(SelectedCount));
        Raise(nameof(HasDuplicates));
        Raise(nameof(HasValidationError));
        Raise(nameof(HasIssue));
        Raise(nameof(IssueMessage));
        Raise(nameof(ValidationMessage));
        Raise(nameof(IsComplete));
        Raise(nameof(StatusText));
        Raise(nameof(HelperText));
        Raise(nameof(CardState));
        Raise(nameof(Subtitle));
    }

    public void SetIssueMessage(string? message)
    {
        var normalized = (message ?? string.Empty).Trim();
        if (string.Equals(_issueMessage, normalized, StringComparison.Ordinal))
            return;

        IssueMessage = normalized;
        RaiseComputed();
    }

    private void UpdateValidation()
    {
        if (Slots.Any(s => s.HasBaseSelection && !s.IsCustomisationComplete))
        {
            ValidationMessage = "Select a custom value for each chosen ability.";
            return;
        }

        if (_selectionValidator == null)
        {
            ValidationMessage = string.Empty;
            return;
        }

        var picked = Slots
            .Select(s => s.SelectionKey)
            .Where(x => x.Length > 0)
            .ToList();

        ValidationMessage = _selectionValidator.Invoke(picked) ?? string.Empty;
    }
}
