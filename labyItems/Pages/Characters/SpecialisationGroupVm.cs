
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using labyItems.Controls;
using labyItems.Controls.Pickers;
using labyItems.Models.Enums;

namespace labyItems.Pages.Characters;

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
    private bool _isOptional;

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

    public int SelectedCount => Slots.Count(s => !string.IsNullOrWhiteSpace(s.SelectedOption));

    public bool HasDuplicates
    {
        get
        {
            var picked = Slots
                .Select(s => (s.SelectedOption ?? "").Trim())
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

    public bool IsComplete
    {
        get
        {
            if (_isOptional)
                return !HasDuplicates && !HasValidationError;

            return SelectedCount == RequiredCount && !HasDuplicates && !HasValidationError;
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
            if (_isOptional && SelectedCount == 0) return "Optional selection.";
            if (SelectedCount == 0) return "Make your selections below.";
            if (HasDuplicates) return "Duplicate selections detected. Choose different abilities for each level.";
            if (IsComplete) return "Selection complete.";
            return "Continue selecting until all levels are filled.";
        }
    }

    public SpecialisationCardState CardState
    {
        get
        {
            if (IsComplete) return SpecialisationCardState.Success;
            if (HasDuplicates || HasValidationError) return SpecialisationCardState.Error;
            return SpecialisationCardState.Neutral;
        }
    }
    public enum SpecialisationCardState
    {
        Neutral,
        Success,
        Error
    }
    public bool UseWardPactEnum { get; }

    public SpecialisationGroupVm(
        string title,
        IEnumerable<int> levels,
        List<string> optionNames,
        Dictionary<int, string> initiallySelectedByLevel,
        Action onAnySelectionChanged,
        bool useWardPactEnum,
        bool useMagicColourEnum = false,
        bool useVivomancerColourEnum = false,
        bool useDictionarySearch = false,
        IEnumerable<MagicColours>? magicColourOptions = null,
        IEnumerable<VivomancerColours>? vivomancerColourOptions = null,
        Func<IReadOnlyList<string>, string?>? selectionValidator = null,
        bool isOptional = false)
    {
        Title = title;
        UseWardPactEnum = useWardPactEnum;
        _useMagicColourEnum = useMagicColourEnum;
        _useVivomancerColourEnum = useVivomancerColourEnum;
        _selectionValidator = selectionValidator;
        _onAnySelectionChanged = onAnySelectionChanged;
        _isOptional = isOptional;

        if (_useMagicColourEnum)
        {
            var opts = magicColourOptions?.ToList() ?? Enum.GetValues<MagicColours>().ToList();
            _allOptionNames = opts
                .Select(EnumDisplayFormatter.Format)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else if (_useVivomancerColourEnum)
        {
            var opts = vivomancerColourOptions?.ToList() ?? Enum.GetValues<VivomancerColours>().ToList();
            _allOptionNames = opts
                .Select(EnumDisplayFormatter.Format)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            _allOptionNames = optionNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        ToggleExpandedCommand = new Command(() => IsExpanded = !IsExpanded);

        foreach (var lvl in levels.OrderBy(x => x))
        {
            initiallySelectedByLevel.TryGetValue(lvl, out var pre);

            var slot = new SpecialisationSlotVm(
                lvl,
                useWardPactEnum,
                _useMagicColourEnum,
                _useVivomancerColourEnum,
                useDictionarySearch,
                () => OnSlotChanged());

            if (_useMagicColourEnum)
            {
                var allowed = magicColourOptions?.ToList() ?? Enum.GetValues<MagicColours>().ToList();
                slot.ConfigureMagicColours(allowed);
                slot.SetOptionsSource(() => slot.MagicColourOptionNames);
                if (!string.IsNullOrWhiteSpace(pre))
                {
                    var match = allowed.FirstOrDefault(m => string.Equals(EnumDisplayFormatter.Format(m), pre, StringComparison.OrdinalIgnoreCase));
                    slot.SelectedMagicColour = match;
                }
            }
            else if (_useVivomancerColourEnum)
            {
                var allowed = vivomancerColourOptions?.ToList() ?? Enum.GetValues<VivomancerColours>().ToList();
                slot.ConfigureVivomancerColours(allowed);
                slot.SetOptionsSource(() => slot.VivomancerColourOptionNames);
                if (!string.IsNullOrWhiteSpace(pre))
                {
                    var match = allowed.FirstOrDefault(m => string.Equals(EnumDisplayFormatter.Format(m), pre, StringComparison.OrdinalIgnoreCase));
                    slot.SelectedVivomancerColour = match;
                }
            }
            else
            {
                slot.SetOptionsSource(() => OptionNames);

                if (useWardPactEnum)
                {
                    if (!string.IsNullOrWhiteSpace(pre))
                    {
                        var match = WardPactOptions.Standard.FirstOrDefault(k => string.Equals(k.Key, pre, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(match.Key))
                            slot.SelectedWardPact = match.Value;
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(pre))
                        slot.SelectedOption = pre;
                }
            }

            Slots.Add(slot);
        }

        UpdateValidation();
        RaiseComputed();
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
        Raise(nameof(ValidationMessage));
        Raise(nameof(IsComplete));
        Raise(nameof(StatusText));
        Raise(nameof(HelperText));
        Raise(nameof(CardState));
        Raise(nameof(Subtitle));
    }

    private void UpdateValidation()
    {
        if (_selectionValidator == null)
        {
            ValidationMessage = string.Empty;
            return;
        }

        var picked = Slots
            .Select(s => (s.SelectedOption ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .ToList();

        ValidationMessage = _selectionValidator.Invoke(picked) ?? string.Empty;
    }
}
