using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Models.Rules;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class EvocationListVm : INotifyPropertyChanged
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

    private const int MaxTotalPower = 60;
    private const int MaxAdvancedPower = 20;

    private readonly IReadOnlyList<DruidEvocationService.EvocRaw> _allEvocations;
    private readonly Dictionary<string, DruidEvocationService.EvocRaw> _evocationLookup;
    private readonly ICharacterAdvancementDomainService _domainService;

    public EvocationListDraft Draft { get; }

    public string Name => Draft.Name;
    public string HeaderTitle =>
        string.IsNullOrWhiteSpace(Draft.Name)
            ? (Draft.IsPost8th ? "post 8th" : "base list")
            : Draft.Name;
    public bool IsPost8th => Draft.IsPost8th;

    public bool IsMinimized
    {
        get => Draft.IsMinimized;
        set
        {
            if (Draft.IsMinimized == value) return;
            Draft.IsMinimized = value;
            Raise();
            Raise(nameof(IsExpanded));
            Raise(nameof(CanAddSelected));
        }
    }

    public bool IsExpanded => !IsMinimized;

    public ObservableCollection<EvocationEntryVm> Entries { get; } = new();

    private EvocationOption? _selectedEvocationOption;
    public EvocationOption? SelectedEvocationOption
    {
        get => _selectedEvocationOption;
        set
        {
            if (!Set(ref _selectedEvocationOption, value)) return;
            Raise(nameof(CanAddSelected));
        }
    }

    public bool CanAddSelected => IsExpanded && SelectedEvocationOption != null;

    public ObservableCollection<string> FieldFilterOptions { get; } = new();
    public ObservableCollection<string> SelectedFieldFilters { get; } = new();
    public ObservableCollection<string> TierFilterOptions { get; } = new() { "Advanced", "Standard" };
    public ObservableCollection<string> SelectedTierFilters { get; } = new();
    public ObservableCollection<EvocationFieldSegmentVm> FieldBreakdownSegments { get; } = new();
    public ObservableCollection<EvocationFieldLegendVm> FieldLegendItems { get; } = new();
    public bool HasFieldBreakdown => FieldBreakdownSegments.Count > 0;

    private bool _showFieldLegend;
    public bool ShowFieldLegend
    {
        get => _showFieldLegend;
        set
        {
            if (!Set(ref _showFieldLegend, value))
                return;
            Raise(nameof(FieldLegendChevronText));
        }
    }

    public string FieldLegendChevronText => ShowFieldLegend ? "▴" : "▾";

    private Dictionary<string, EvocationOption> _filteredOptions = new();
    public Dictionary<string, EvocationOption> FilteredOptions
    {
        get => _filteredOptions;
        private set => Set(ref _filteredOptions, value);
    }

    private int _totalPower;
    public int TotalPower
    {
        get => _totalPower;
        private set => Set(ref _totalPower, value);
    }

    private int _advancedPower;
    public int AdvancedPower
    {
        get => _advancedPower;
        private set => Set(ref _advancedPower, value);
    }

    private string _validationMessage = string.Empty;
    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (Set(ref _validationMessage, value))
                Raise(nameof(HasValidationError));
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    public ICommand AddSelectedCommand { get; }
    public ICommand RemoveEntryCommand { get; }
    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleFieldLegendCommand { get; }

    public EvocationListVm(
        EvocationListDraft draft,
        IReadOnlyList<DruidEvocationService.EvocRaw> allEvocations,
        ICharacterAdvancementDomainService domainService)
    {
        Draft = draft;
        _allEvocations = allEvocations ?? Array.Empty<DruidEvocationService.EvocRaw>();
        _domainService = domainService;
        _evocationLookup = _allEvocations
            .Where(e => !string.IsNullOrWhiteSpace(e?.name))
            .GroupBy(e => e.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        AddSelectedCommand = new Command(AddSelectedEvocation);
        RemoveEntryCommand = new Command<EvocationEntryVm>(RemoveEntry);
        ToggleExpandedCommand = new Command(() => IsMinimized = !IsMinimized);
        ToggleFieldLegendCommand = new Command(() => ShowFieldLegend = !ShowFieldLegend);

        SelectedFieldFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        SelectedTierFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        FieldBreakdownSegments.CollectionChanged += (_, __) => Raise(nameof(HasFieldBreakdown));

        LoadFieldOptions();
        LoadEntriesFromDraft();
        UpdateFilteredOptions();
        UpdateStats();
    }

    private void LoadFieldOptions()
    {
        FieldFilterOptions.Clear();
        foreach (var field in EvocationFieldCatalog.Definitions)
            FieldFilterOptions.Add(field.DisplayName);
    }

    private void LoadEntriesFromDraft()
    {
        Entries.Clear();
        foreach (var entry in Draft.Entries ?? new List<EvocationListEntryDraft>())
        {
            var vm = new EvocationEntryVm(entry, OnEntryChanged);
            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                if (_evocationLookup.TryGetValue(entry.Name, out var match))
                {
                    vm.SelectedEvocation = new EvocationOption(match.name, match.power, match.isAdvanced, match.fields);
                }
                else
                {
                    vm.SelectedEvocation = new EvocationOption(entry.Name, entry.Power, entry.IsAdvanced, Array.Empty<string>());
                }
            }
            Entries.Add(vm);
        }
        ReindexEntries();
    }

    private void AddSelectedEvocation()
    {
        if (SelectedEvocationOption == null)
            return;

        var draft = new EvocationListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new EvocationEntryVm(draft, OnEntryChanged);
        vm.SelectedEvocation = SelectedEvocationOption.Value;
        Entries.Add(vm);
        ReindexEntries();
        SearchPickerStateHelper.ClearForNextSearch<EvocationOption>(
            setSelection: v => SelectedEvocationOption = v,
            setSearchText: _ => { });
        UpdateStats();
    }

    private void RemoveEntry(EvocationEntryVm? entry)
    {
        if (entry == null) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        ReindexEntries();
        UpdateStats();
    }

    private void OnEntryChanged()
    {
        UpdateFilteredOptions();
        UpdateStats();
    }

    private void UpdateFilteredOptions()
    {
        var dict = new Dictionary<string, EvocationOption>(StringComparer.OrdinalIgnoreCase);
        var selectedFields = SelectedFieldFilters != null
            ? new HashSet<string>(SelectedFieldFilters, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedTiers = SelectedTierFilters != null
            ? new HashSet<string>(SelectedTierFilters, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filterByFields = selectedFields.Count > 0;
        var filterAdvanced = selectedTiers.Contains("Advanced");
        var filterStandard = selectedTiers.Contains("Standard");
        var filterByTier = selectedTiers.Count == 1;

        foreach (var ev in _allEvocations)
        {
            if (string.IsNullOrWhiteSpace(ev?.name))
                continue;

            if (filterByFields)
            {
                if (!EvocationFieldCatalog.MatchesAnySelectedField(ev.fields, selectedFields))
                    continue;
            }

            if (filterByTier)
            {
                if (filterAdvanced && !ev.isAdvanced)
                    continue;
                if (filterStandard && ev.isAdvanced)
                    continue;
            }

            var option = new EvocationOption(ev.name, ev.power, ev.isAdvanced, ev.fields ?? new List<string>());
            var label = $"{ev.name} ({ev.power})";
            if (!dict.ContainsKey(label))
                dict[label] = option;
        }

        FilteredOptions = dict;
    }

    private void ReindexEntries()
    {
        for (var i = 0; i < Entries.Count; i++)
            Entries[i].SetRowIndex(i);
    }

    private void UpdateStats()
    {
        var totals = _domainService.ComputeEvocationPointTotals(Draft.Entries);
        var total = totals.Total;
        var advanced = totals.Advanced;

        TotalPower = total;
        AdvancedPower = advanced;

        var message = string.Empty;
        if (total > MaxTotalPower)
        {
            message = $"Total evocation power exceeds {MaxTotalPower} EP.";
        }
        else if (advanced > MaxAdvancedPower)
        {
            message = $"Advanced evocations exceed {MaxAdvancedPower} EP.";
        }
        else
        {
            var advancedEntries = Draft.Entries
                .Where(e => e.IsAdvanced && !string.IsNullOrWhiteSpace(e.Name))
                .ToList();

            if (advancedEntries.Count > 1)
            {
                var allShareField = _domainService.AdvancedEvocationsShareAField(
                    advancedEntries,
                    name =>
                    {
                        if (!_evocationLookup.TryGetValue(name, out var ev))
                            return null;
                        return EvocationFieldCatalog.GetComparableFieldKeys(ev.fields);
                    });

                if (!allShareField)
                    message = "Advanced evocations must all come from the same field.";
            }
        }

        RebuildFieldBreakdown();
        ValidationMessage = message;
    }

    private void RebuildFieldBreakdown()
    {
        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Draft.Entries ?? new List<EvocationListEntryDraft>())
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            var power = Math.Max(0, entry.Power);
            if (power <= 0)
                continue;

            if (!_evocationLookup.TryGetValue(entry.Name, out var evocation))
                continue;

            var fields = EvocationFieldCatalog.ResolveFields(evocation.fields);
            if (fields.Count == 0)
                continue;

            var splitPower = power / (double)fields.Count;
            foreach (var field in fields)
                totals[field.Key] = totals.GetValueOrDefault(field.Key, 0d) + splitPower;
        }

        FieldBreakdownSegments.Clear();
        FieldLegendItems.Clear();

        if (totals.Count == 0)
        {
            ShowFieldLegend = false;
            return;
        }

        var ordered = EvocationFieldCatalog.Definitions
            .Where(def => totals.TryGetValue(def.Key, out var value) && value > 0d)
            .ToList();

        var totalWeight = ordered.Sum(def => totals[def.Key]);
        foreach (var field in ordered)
        {
            var value = totals[field.Key];
            var ratio = totalWeight > 0d ? (value / totalWeight) * 100d : 0d;

            FieldBreakdownSegments.Add(new EvocationFieldSegmentVm(Math.Max(0.01, value), field.Colour));
            FieldLegendItems.Add(new EvocationFieldLegendVm(
                field.DisplayName,
                field.Colour,
                $"{FormatFieldPower(value)} EP ({ratio:0.#}%)"));
        }
    }

    private static string FormatFieldPower(double value)
    {
        var rounded = Math.Round(value, 1);
        if (Math.Abs(rounded - Math.Round(rounded)) < 0.01)
            return ((int)Math.Round(rounded)).ToString();
        return rounded.ToString("0.0");
    }
}

public sealed class EvocationEntryVm : INotifyPropertyChanged
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

    public EvocationListEntryDraft Draft { get; }

    public string DisplayText
        => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : $"{Draft.Name} ({Draft.Power} EP)";
    public string NameText => Draft.Name ?? string.Empty;
    public string PowerText => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : $"{Draft.Power} EP";

    public bool HasEvocation => !string.IsNullOrWhiteSpace(Draft.Name);

    private int _rowIndex;
    public Color RowBackgroundColor => (_rowIndex % 2) == 0 ? Colors.White : Color.FromArgb("#FAF8F3");

    private EvocationOption? _selectedEvocation;
    public EvocationOption? SelectedEvocation
    {
        get => _selectedEvocation;
        set
        {
            if (!Set(ref _selectedEvocation, value)) return;

            if (value == null)
            {
                Draft.Name = string.Empty;
                Draft.Power = 0;
                Draft.IsAdvanced = false;
            }
            else if (value is EvocationOption option)
            {
                Draft.Name = option.Name;
                Draft.Power = option.Power;
                Draft.IsAdvanced = option.IsAdvanced;
            }

            Raise(nameof(DisplayText));
            Raise(nameof(NameText));
            Raise(nameof(PowerText));
            Raise(nameof(HasEvocation));
            _onChanged();
        }
    }

    public EvocationEntryVm(EvocationListEntryDraft draft, Action onChanged)
    {
        Draft = draft;
        _onChanged = onChanged;
    }

    public void SetRowIndex(int rowIndex)
    {
        if (_rowIndex == rowIndex)
            return;

        _rowIndex = rowIndex;
        Raise(nameof(RowBackgroundColor));
    }
}

public sealed class EvocationFieldSegmentVm
{
    public double Weight { get; }
    public Color Colour { get; }

    public EvocationFieldSegmentVm(double weight, Color colour)
    {
        Weight = weight;
        Colour = colour;
    }
}

public sealed class EvocationFieldLegendVm
{
    public string FieldName { get; }
    public Color Colour { get; }
    public string SummaryText { get; }

    public EvocationFieldLegendVm(string fieldName, Color colour, string summaryText)
    {
        FieldName = fieldName;
        Colour = colour;
        SummaryText = summaryText;
    }
}

internal sealed class EvocationFieldDefinition
{
    public EvocationFields Field { get; }
    public string DisplayName { get; }
    public string Key { get; }
    public Color Colour { get; }

    public EvocationFieldDefinition(EvocationFields field, string displayName, string key, Color colour)
    {
        Field = field;
        DisplayName = displayName;
        Key = key;
        Colour = colour;
    }
}

internal static class EvocationFieldCatalog
{
    private const string AllToken = "all";

    public static IReadOnlyList<EvocationFieldDefinition> Definitions { get; } = new List<EvocationFieldDefinition>
    {
        BuildDefinition(EvocationFields.Spring, "Spring", "#22C55E"),
        BuildDefinition(EvocationFields.Summer, "Summer", "#F59E0B"),
        BuildDefinition(EvocationFields.Autumn, "Autumn", "#B45309"),
        BuildDefinition(EvocationFields.Winter, "Winter", "#60A5FA"),
        BuildDefinition(EvocationFields.HornedMan, "Horned Man", "#7C3AED"),
        BuildDefinition(EvocationFields.MotherNature, "Mother Nature", "#16A34A"),
        BuildDefinition(EvocationFields.ShadowOfTheDawn, "Shadow of the Dawn", "#4B5563"),
        BuildDefinition(EvocationFields.KeeperOfWinds, "Keeper of the Winds", "#0EA5E9"),
        BuildDefinition(EvocationFields.FatherOfTheWorld, "Father of the World", "#92400E"),
        BuildDefinition(EvocationFields.Deserts, "Deserts", "#D97706"),
        BuildDefinition(EvocationFields.Skies, "Skies", "#38BDF8"),
        BuildDefinition(EvocationFields.Mountains, "Mountains", "#6B7280"),
        BuildDefinition(EvocationFields.Rivers, "Rivers", "#0F766E"),
        BuildDefinition(EvocationFields.Forests, "Forests", "#166534"),
        BuildDefinition(EvocationFields.Cities, "Cities", "#334155")
    };

    private static readonly Dictionary<string, EvocationFieldDefinition> DefinitionLookup = BuildLookup();

    private static readonly Dictionary<string, string> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forest"] = NormalizeToken(EvocationFields.Forests.ToString()),
        ["river"] = NormalizeToken(EvocationFields.Rivers.ToString()),
        ["mountain"] = NormalizeToken(EvocationFields.Mountains.ToString()),
        ["desert"] = NormalizeToken(EvocationFields.Deserts.ToString()),
        ["city"] = NormalizeToken(EvocationFields.Cities.ToString()),
        ["sky"] = NormalizeToken(EvocationFields.Skies.ToString()),
        ["keeperofthewind"] = NormalizeToken(EvocationFields.KeeperOfWinds.ToString())
    };

    public static bool MatchesAnySelectedField(IEnumerable<string>? rawFields, HashSet<string> selectedDisplayNames)
    {
        if (selectedDisplayNames.Count == 0)
            return true;

        var selectedKeys = new HashSet<string>(
            selectedDisplayNames.Select(NormalizeToken),
            StringComparer.OrdinalIgnoreCase);

        var keys = ExtractComparableKeys(rawFields, includeAllAsEveryField: true);
        return keys.Overlaps(selectedKeys);
    }

    public static HashSet<string> GetComparableFieldKeys(IEnumerable<string>? rawFields)
        => ExtractComparableKeys(rawFields, includeAllAsEveryField: true);

    public static IReadOnlyList<EvocationFieldDefinition> ResolveFields(IEnumerable<string>? rawFields)
    {
        var keys = ExtractComparableKeys(rawFields, includeAllAsEveryField: true);
        if (keys.Count == 0)
            return Array.Empty<EvocationFieldDefinition>();

        return Definitions
            .Where(def => keys.Contains(def.Key))
            .ToList();
    }

    private static HashSet<string> ExtractComparableKeys(IEnumerable<string>? rawFields, bool includeAllAsEveryField)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasAll = false;

        foreach (var rawField in rawFields ?? Array.Empty<string>())
        {
            var normalized = NormalizeToken(rawField);
            if (normalized.Length == 0)
                continue;

            if (string.Equals(normalized, AllToken, StringComparison.OrdinalIgnoreCase))
            {
                hasAll = true;
                continue;
            }

            if (!DefinitionLookup.TryGetValue(normalized, out var definition)
                && Synonyms.TryGetValue(normalized, out var canonicalKey))
            {
                DefinitionLookup.TryGetValue(canonicalKey, out definition);
            }

            if (definition != null)
                keys.Add(definition.Key);
        }

        if (hasAll && includeAllAsEveryField)
        {
            foreach (var definition in Definitions)
                keys.Add(definition.Key);
        }

        return keys;
    }

    private static EvocationFieldDefinition BuildDefinition(EvocationFields field, string displayName, string colourHex)
    {
        var key = NormalizeToken(field.ToString());
        return new EvocationFieldDefinition(field, displayName, key, Color.FromArgb(colourHex));
    }

    private static Dictionary<string, EvocationFieldDefinition> BuildLookup()
    {
        var lookup = new Dictionary<string, EvocationFieldDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            lookup[definition.Key] = definition;
            lookup[NormalizeToken(definition.DisplayName)] = definition;
        }

        return lookup;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();

        return new string(chars);
    }
}

public readonly record struct EvocationOption(string Name, int Power, bool IsAdvanced, IReadOnlyList<string> Fields)
{
    public bool Equals(EvocationOption other)
        => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
           && Power == other.Power
           && IsAdvanced == other.IsAdvanced;

    public override int GetHashCode()
        => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Name ?? string.Empty), Power, IsAdvanced);
}

public readonly record struct ManuAbilityOption(
    string Name,
    int Cost,
    int Table,
    string Availability,
    IReadOnlyList<RuleClause> AvailabilityRules,
    string Description)
{
    public bool Equals(ManuAbilityOption other)
        => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
           && Cost == other.Cost
           && Table == other.Table;

    public override int GetHashCode()
        => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Name ?? string.Empty), Cost, Table);
}
