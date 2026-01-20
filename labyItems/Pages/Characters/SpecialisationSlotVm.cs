using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using labyItems.Controls;
using labyItems.Controls.Pickers;
using labyItems.Models.Enums;

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

    public int Level { get; }
    public string LevelLabel => $"Lvl {Level}";

    public bool UseWardPactEnum { get; }
    public bool UseMagicColourEnum { get; }
    public bool UseVivomancerColourEnum { get; }

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
                _suppressSelectionSync = false;
            }

            _onChanged();
        }
    }

    private Func<IReadOnlyList<string>>? _getFilteredOptions;
    public IReadOnlyList<string> FilteredOptionNames => _getFilteredOptions?.Invoke() ?? Array.Empty<string>();

    private bool _suppressSelectionSync;

    public SpecialisationSlotVm(
        int level,
        bool useWardPactEnum,
        bool useMagicColourEnum,
        bool useVivomancerColourEnum,
        Action onChanged)
    {
        Level = level;
        UseWardPactEnum = useWardPactEnum;
        UseMagicColourEnum = useMagicColourEnum;
        UseVivomancerColourEnum = useVivomancerColourEnum;
        _onChanged = onChanged;
    }

    public void SetOptionsSource(Func<IReadOnlyList<string>> getFilteredOptions)
    {
        _getFilteredOptions = getFilteredOptions;
        Raise(nameof(FilteredOptionNames));
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
    }

    public void RaiseFilteredOptionsChanged()
    {
        Raise(nameof(FilteredOptionNames));
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

    private static string LabelForMagicColour(MagicColours c)
        => EnumDisplayFormatter.FormatName(c.ToString());

    private static string LabelForVivomancerColour(VivomancerColours c)
        => EnumDisplayFormatter.FormatName(c.ToString());
}
