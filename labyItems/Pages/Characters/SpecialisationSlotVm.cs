using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using labyItems.Controls;
using labyItems.Controls.Pickers;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Helpers;

namespace labyItems.Pages.Characters;

public sealed class SpecialisationSlotVm : INotifyPropertyChanged
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

    private readonly Action _onChanged;
    private readonly Func<string?, AbilityCustomisation?>? _customisationResolver;
    private readonly Func<AbilityCustomisation?, Dictionary<string, string>?>? _customisationOptionsProvider;
    private readonly bool _hideAbilityPickerWhenSingleOption;
    private string _specialisationKeyForDetails = string.Empty;

    public int Level { get; }
    public string LevelLabel => $"Lvl {Level}";

    public bool UseWardPactEnum => Mode == SlotOptionMode.WardPactEnum;
    public bool UseDictionarySearch => Mode == SlotOptionMode.DictionarySearch;
    public bool UseMagicColourEnum => Mode == SlotOptionMode.EnumPicker && EnumType == typeof(MagicColours);
    public bool UseVivomancerColourEnum => Mode == SlotOptionMode.EnumPicker && EnumType == typeof(VivomancerColours);

    public bool HideAbilityPicker => _hideAbilityPickerWhenSingleOption && FilteredOptionNames.Count <= 1;

    private AbilityDefinition? _forcedAbilityDefinition;
    private string _lockedDisplayText = string.Empty;

    public AbilityDefinition? ForcedAbilityDefinition => _forcedAbilityDefinition;
    public bool IsLocked => _forcedAbilityDefinition != null;
    public bool IsSelectable => !IsLocked;
    public string LockedDisplayText => _lockedDisplayText;

    private StandardWardPacts? _selectedWardPact;
    public StandardWardPacts? SelectedWardPact
    {
        get => _selectedWardPact;
        set
        {
            if (!Set(ref _selectedWardPact, value)) return;

            if (value is StandardWardPacts v)
                SelectedOption = WardPactOptions.LabelFor(v);
            else
                SelectedOption = null;
        }
    }

    private MagicColours? _selectedMagicColour;
    public MagicColours? SelectedMagicColour
    {
        get => _selectedMagicColour;
        set
        {
            if (!Set(ref _selectedMagicColour, value)) return;

            if (_suppressSelectionSync) return;

            _suppressSelectionSync = true;
            SelectedOption = value is MagicColours v ? LabelForMagicColour(v) : null;
            _suppressSelectionSync = false;
        }
    }

    private VivomancerColours? _selectedVivomancerColour;
    public VivomancerColours? SelectedVivomancerColour
    {
        get => _selectedVivomancerColour;
        set
        {
            if (!Set(ref _selectedVivomancerColour, value)) return;

            if (_suppressSelectionSync) return;

            _suppressSelectionSync = true;
            SelectedOption = value is VivomancerColours v ? LabelForVivomancerColour(v) : null;
            _suppressSelectionSync = false;
        }
    }

    private readonly List<MagicColours> _magicColourOrder = new();
    private readonly List<VivomancerColours> _vivomancerColourOrder = new();
    private Dictionary<string, MagicColours> _magicColourOptions = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, VivomancerColours> _vivomancerColourOptions = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, MagicColours> MagicColourOptions => _magicColourOptions;
    public Dictionary<string, VivomancerColours> VivomancerColourOptions => _vivomancerColourOptions;
    public IReadOnlyList<string> MagicColourOptionNames => _magicColourOptions.Keys.ToList();
    public IReadOnlyList<string> VivomancerColourOptionNames => _vivomancerColourOptions.Keys.ToList();

    private AbilityCustomisation? _customisation;
    private string? _customisationValue;
    private Dictionary<string, string> _customisationOptions = new(StringComparer.OrdinalIgnoreCase);
    private bool _customisationAllowsCustom;
    private string _customisationPlaceholder = "Enter value";
    private string _customisationEnumName = string.Empty;

    public bool HasCustomisation => _customisation != null;
    public bool CustomisationAllowsCustom => _customisationAllowsCustom;
    public string CustomisationPlaceholder => _customisationPlaceholder;
    public Dictionary<string, string> CustomisationOptions => _customisationOptions;
    public bool ShowCustomisationPicker => HasCustomisation && (CustomisationAllowsCustom || _customisationOptions.Count > 0);
    public bool IsWeaponMasterySelection
    {
        get
        {
            var selected = (_selectedOption ?? string.Empty).Trim();
            if (selected.Length == 0)
                return false;

            return selected.Contains("Weapon Mastery", StringComparison.OrdinalIgnoreCase);
        }
    }
    public bool ShowCustomisationInline => ShowCustomisationPicker && IsWeaponMasterySelection && !UseDictionarySearch;
    public bool ShowCustomisationBelow => ShowCustomisationPicker && !ShowCustomisationInline && !UseDictionarySearch;

    public string? CustomisationValue
    {
        get => _customisationValue;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            normalized = normalized.Length == 0 ? null : normalized;
            if (!Set(ref _customisationValue, normalized)) return;
            Raise(nameof(IsCustomisationComplete));
            Raise(nameof(HasSelection));
            Raise(nameof(SelectionKey));
            _onChanged();
        }
    }

    public bool IsCustomisationComplete
        => _customisation == null || !string.IsNullOrWhiteSpace(_customisationValue);

    public bool HasBaseSelection
        => IsLocked || !string.IsNullOrWhiteSpace(_selectedOption);

    public bool HasSelection
        => HasBaseSelection && IsCustomisationComplete;

    public string SelectedAbilityNameForDetails
    {
        get
        {
            if (UseMagicColourEnum || UseVivomancerColourEnum || UseWardPactEnum)
                return UseWardPactEnum ? "Ward pact" : string.Empty;

            if (IsLocked)
                return ((ForcedAbilityDefinition?.Name ?? _lockedDisplayText) ?? string.Empty).Trim();

            return (_selectedOption ?? string.Empty).Trim();
        }
    }

    public bool HasSelectedAbilityForDetails
        => UseWardPactEnum || !string.IsNullOrWhiteSpace(SelectedAbilityNameForDetails);

    public string SpecialisationKeyForDetails => _specialisationKeyForDetails;

    public string SelectionKey
    {
        get
        {
            if (!HasSelection)
                return string.Empty;

            if (IsLocked && _forcedAbilityDefinition != null)
                return _forcedAbilityDefinition.Name ?? _lockedDisplayText;

            var baseName = _selectedOption ?? string.Empty;
            var custom = _customisationValue?.Trim();
            if (!string.IsNullOrWhiteSpace(custom))
                return $"{baseName}::{custom}";

            return baseName;
        }
    }

    private string? _selectedOption;
    public string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            if (!Set(ref _selectedOption, normalized)) return;

            if (!_suppressSelectionSync)
            {
                _suppressSelectionSync = true;
                if (UseMagicColourEnum)
                {
                    SelectedMagicColour = FindMagicColour(normalized);
                }
                else if (UseVivomancerColourEnum)
                {
                    SelectedVivomancerColour = FindVivomancerColour(normalized);
                }
                else if (UseWardPactEnum)
                {
                    SelectedWardPact = FindWardPact(normalized);
                }
                _suppressSelectionSync = false;
            }

            ApplyCustomisation(_customisationResolver?.Invoke(normalized));
            Raise(nameof(IsWeaponMasterySelection));
            Raise(nameof(ShowCustomisationInline));
            Raise(nameof(ShowCustomisationBelow));
            Raise(nameof(SelectedAbilityNameForDetails));
            Raise(nameof(HasSelectedAbilityForDetails));
            _onChanged();
        }
    }

    private Func<IReadOnlyList<string>>? _getFilteredOptions;
    public IReadOnlyList<string> FilteredOptionNames => _getFilteredOptions?.Invoke() ?? Array.Empty<string>();
    public Dictionary<string, string> SearchOptions
        => FilteredOptionNames.ToDictionary(o => o, o => o, StringComparer.OrdinalIgnoreCase);

    private bool _suppressSelectionSync;

    public SpecialisationSlotVm(
     int level,
     SlotOptionMode mode,
     Type? enumType,
     Action onChanged,
     Func<string?, AbilityCustomisation?>? customisationResolver = null,
     Func<AbilityCustomisation?, Dictionary<string, string>?>? customisationOptionsProvider = null,
     bool hideAbilityPickerWhenSingleOption = true)
    {
        Level = level;
        Mode = mode;
        EnumType = enumType;
        _onChanged = onChanged;
        _customisationResolver = customisationResolver;
        _customisationOptionsProvider = customisationOptionsProvider;
        _hideAbilityPickerWhenSingleOption = hideAbilityPickerWhenSingleOption;
    }

    public SlotOptionMode Mode { get; }
    public Type? EnumType { get; }

    public void SetOptionsSource(Func<IReadOnlyList<string>> getFilteredOptions)
    {
        _getFilteredOptions = getFilteredOptions;
        Raise(nameof(FilteredOptionNames));
        Raise(nameof(SearchOptions));
        Raise(nameof(HideAbilityPicker));
    }

    public void SetSpecialisationKeyForDetails(string? key)
    {
        _specialisationKeyForDetails = (key ?? string.Empty).Trim();
    }

    public void ConfigureMagicColours(IEnumerable<MagicColours> allowed)
    {
        _magicColourOrder.Clear();
        _magicColourOrder.AddRange(allowed ?? Array.Empty<MagicColours>());

        _magicColourOptions = _magicColourOrder
            .Distinct()
            .ToDictionary(LabelForMagicColour, v => v, StringComparer.OrdinalIgnoreCase);

        Raise(nameof(MagicColourOptions));
        Raise(nameof(MagicColourOptionNames));

        if (SelectedMagicColour is MagicColours m && !_magicColourOptions.Values.Contains(m))
            SelectedMagicColour = null;

        if (!string.IsNullOrWhiteSpace(SelectedOption) && !MagicColourOptions.ContainsKey(SelectedOption))
            SelectedOption = null;

        RaiseFilteredOptionsChanged();
    }

    public void ConfigureVivomancerColours(IEnumerable<VivomancerColours> allowed)
    {
        _vivomancerColourOrder.Clear();
        _vivomancerColourOrder.AddRange(allowed ?? Array.Empty<VivomancerColours>());

        _vivomancerColourOptions = _vivomancerColourOrder
            .Distinct()
            .ToDictionary(LabelForVivomancerColour, v => v, StringComparer.OrdinalIgnoreCase);

        Raise(nameof(VivomancerColourOptions));
        Raise(nameof(VivomancerColourOptionNames));

        if (SelectedVivomancerColour is VivomancerColours v && !_vivomancerColourOptions.Values.Contains(v))
            SelectedVivomancerColour = null;

        if (!string.IsNullOrWhiteSpace(SelectedOption) && !VivomancerColourOptions.ContainsKey(SelectedOption))
            SelectedOption = null;

        RaiseFilteredOptionsChanged();
    }

    public void RaiseFilteredOptionsChanged()
    {
        Raise(nameof(FilteredOptionNames));
        Raise(nameof(SearchOptions));
        Raise(nameof(HideAbilityPicker));
    }

    public bool ApplyForcedAbility(AbilityDefinition def, string? displayText = null, bool suppressNotify = false)
    {
        if (def == null)
            return ClearForcedAbility(suppressNotify);

        var display = (displayText ?? def.Name ?? string.Empty).Trim();
        var changed = _forcedAbilityDefinition == null
                      || !string.Equals(_forcedAbilityDefinition.Name ?? string.Empty, def.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                      || !string.Equals(_lockedDisplayText, display, StringComparison.Ordinal);

        _forcedAbilityDefinition = def;
        _lockedDisplayText = display;

        if (!string.IsNullOrWhiteSpace(_selectedOption))
            _selectedOption = null;

        if (_selectedWardPact.HasValue)
            _selectedWardPact = null;
        if (_selectedMagicColour.HasValue)
            _selectedMagicColour = null;
        if (_selectedVivomancerColour.HasValue)
            _selectedVivomancerColour = null;

        if (_customisation != null || !string.IsNullOrWhiteSpace(_customisationValue))
        {
            _customisation = null;
            _customisationValue = null;
            _customisationOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _customisationAllowsCustom = false;
            _customisationEnumName = string.Empty;
            _customisationPlaceholder = "Enter value";
        }

        Raise(nameof(SelectedOption));
        Raise(nameof(SelectedWardPact));
        Raise(nameof(SelectedMagicColour));
        Raise(nameof(SelectedVivomancerColour));
        Raise(nameof(HasSelection));
        Raise(nameof(SelectionKey));
        Raise(nameof(IsLocked));
        Raise(nameof(IsSelectable));
        Raise(nameof(LockedDisplayText));
        Raise(nameof(HasCustomisation));
        Raise(nameof(CustomisationOptions));
        Raise(nameof(CustomisationAllowsCustom));
        Raise(nameof(CustomisationPlaceholder));
        Raise(nameof(CustomisationValue));
        Raise(nameof(ShowCustomisationPicker));
        Raise(nameof(ShowCustomisationInline));
        Raise(nameof(ShowCustomisationBelow));
        Raise(nameof(IsCustomisationComplete));
        Raise(nameof(SelectedAbilityNameForDetails));
        Raise(nameof(HasSelectedAbilityForDetails));

        if (changed && !suppressNotify)
            _onChanged();

        return changed;
    }

    public bool ClearForcedAbility(bool suppressNotify = false)
    {
        if (_forcedAbilityDefinition == null)
            return false;

        _forcedAbilityDefinition = null;
        _lockedDisplayText = string.Empty;

        Raise(nameof(IsLocked));
        Raise(nameof(IsSelectable));
        Raise(nameof(LockedDisplayText));
        Raise(nameof(HasSelection));
        Raise(nameof(SelectionKey));
        Raise(nameof(SelectedAbilityNameForDetails));
        Raise(nameof(HasSelectedAbilityForDetails));

        if (!suppressNotify)
            _onChanged();

        return true;
    }

    private MagicColours? FindMagicColour(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        if (_magicColourOptions.TryGetValue(label, out var match))
            return match;

        var fallback = _magicColourOptions
            .FirstOrDefault(kvp => string.Equals(kvp.Key, label, StringComparison.OrdinalIgnoreCase));

        return !EqualityComparer<KeyValuePair<string, MagicColours>>.Default.Equals(fallback, default)
            ? fallback.Value
            : null;
    }

    private VivomancerColours? FindVivomancerColour(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        if (_vivomancerColourOptions.TryGetValue(label, out var match))
            return match;

        var fallback = _vivomancerColourOptions
            .FirstOrDefault(kvp => string.Equals(kvp.Key, label, StringComparison.OrdinalIgnoreCase));

        return !EqualityComparer<KeyValuePair<string, VivomancerColours>>.Default.Equals(fallback, default)
            ? fallback.Value
            : null;
    }

    private StandardWardPacts? FindWardPact(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        if (WardPactOptions.Standard.TryGetValue(label, out var match))
            return match;

        var fallback = WardPactOptions.Standard
            .FirstOrDefault(kvp => string.Equals(kvp.Key, label, StringComparison.OrdinalIgnoreCase));

        return !EqualityComparer<KeyValuePair<string, StandardWardPacts>>.Default.Equals(fallback, default)
            ? fallback.Value
            : null;
    }

    private static string LabelForMagicColour(MagicColours c)
        => EnumDisplayFormatter.FormatName(c.ToString());

    private static string LabelForVivomancerColour(VivomancerColours c)
        => EnumDisplayFormatter.FormatName(c.ToString());

    private void ApplyCustomisation(AbilityCustomisation? customisation)
    {
        if (_customisation == customisation)
            return;

        _customisation = customisation;
        _customisationEnumName = (customisation?.OptionEnum ?? string.Empty).Trim();

        if (_customisation == null)
        {
            _customisationOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _customisationAllowsCustom = false;
            _customisationPlaceholder = "Enter value";
            _customisationValue = null;
        }
        else
        {
            var overrideOptions = _customisationOptionsProvider?.Invoke(_customisation);
            if (overrideOptions == null)
                overrideOptions = BuildCustomisationOptions(_customisationEnumName);

            UpdateCustomisationOptions(overrideOptions);
        }

        Raise(nameof(HasCustomisation));
        Raise(nameof(CustomisationOptions));
        Raise(nameof(CustomisationAllowsCustom));
        Raise(nameof(CustomisationPlaceholder));
        Raise(nameof(CustomisationValue));
        Raise(nameof(IsCustomisationComplete));
        Raise(nameof(HasSelection));
        Raise(nameof(SelectionKey));
        Raise(nameof(ShowCustomisationPicker));
        Raise(nameof(ShowCustomisationInline));
        Raise(nameof(ShowCustomisationBelow));
    }

    public void RefreshCustomisationOptions()
    {
        if (_customisation == null || _customisationOptionsProvider == null)
            return;

        var overrideOptions = _customisationOptionsProvider.Invoke(_customisation);
        if (overrideOptions == null)
            return;

        UpdateCustomisationOptions(overrideOptions);
    }

    private void UpdateCustomisationOptions(Dictionary<string, string> options)
    {
        var normalized = options ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _customisationOptions = new Dictionary<string, string>(normalized, StringComparer.OrdinalIgnoreCase);

        var isSpellCustomisation = IsSpellCustomisation(_customisationEnumName);
        _customisationAllowsCustom = _customisation?.CustomValuesPermitted == true
                                     || (!isSpellCustomisation
                                         && (string.IsNullOrWhiteSpace(_customisationEnumName) || _customisationOptions.Count == 0));
        _customisationPlaceholder = BuildCustomisationPlaceholder(_customisationEnumName, _customisationOptions, isSpellCustomisation);

        var previousValue = _customisationValue;
        if (!_customisationAllowsCustom
            && !string.IsNullOrWhiteSpace(_customisationValue)
            && !_customisationOptions.ContainsKey(_customisationValue))
        {
            _customisationValue = null;
        }

        if (string.IsNullOrWhiteSpace(_customisationValue))
            _customisationValue = null;

        Raise(nameof(CustomisationOptions));
        Raise(nameof(CustomisationAllowsCustom));
        Raise(nameof(CustomisationPlaceholder));
        Raise(nameof(CustomisationValue));
        Raise(nameof(IsCustomisationComplete));
        Raise(nameof(HasSelection));
        Raise(nameof(SelectionKey));
        Raise(nameof(ShowCustomisationPicker));
        Raise(nameof(ShowCustomisationInline));
        Raise(nameof(ShowCustomisationBelow));

        if (!string.Equals(previousValue, _customisationValue, StringComparison.Ordinal))
            _onChanged();
    }

    private static bool IsSpellCustomisation(string enumName)
    {
        if (string.IsNullOrWhiteSpace(enumName))
            return false;

        var trimmed = enumName.Trim();
        return trimmed.StartsWith("SpellUpTo:", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("Spell:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseSpellMaxLevel(string enumName, out int maxLevel)
    {
        maxLevel = 0;
        if (string.IsNullOrWhiteSpace(enumName))
            return false;

        var trimmed = enumName.Trim();
        var parts = trimmed.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        return int.TryParse(parts[1], out maxLevel) && maxLevel > 0;
    }

    private static string BuildCustomisationPlaceholder(string enumName, Dictionary<string, string> options, bool isSpellCustomisation)
    {
        if (isSpellCustomisation)
        {
            if (TryParseSpellMaxLevel(enumName, out var max))
                return $"Select spell (max lvl {max})";

            return "Select spell";
        }

        return string.IsNullOrWhiteSpace(enumName) || options.Count == 0
            ? "Enter value"
            : $"Select {EnumDisplayFormatter.FormatName(enumName)}";
    }

    private static Dictionary<string, string> BuildCustomisationOptions(string enumName)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(enumName))
            return options;

        var type = FindEnumTypeByName(enumName);
        if (type == null)
            return options;

        foreach (var value in Enum.GetValues(type))
        {
            var raw = value?.ToString() ?? string.Empty;
            if (raw.Length == 0)
                continue;

            var label = EnumDisplayFormatter.FormatName(raw);
            options[label] = label;
        }

        return options;
    }

    private static Type? FindEnumTypeByName(string enumName)
        => ReflectionHelper.FindEnumTypeByName(enumName);
}
