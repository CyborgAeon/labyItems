using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class EvilStairwayVm : INotifyPropertyChanged
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

    private const int MaxSpiritTotal = 40;
    private const int RequiredCausingSpirits = 20;

    private readonly IReadOnlyList<MiracleService.MiracRaw> _allMiracles;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _preReqs;
    private readonly HashSet<string>? _churchMiracleNames;
    private readonly Dictionary<string, string> _sphereLookup;
    private readonly Action _onValidationChanged;

    public MiracleListDraft Draft { get; }

    public ObservableCollection<MiracleEntryVm> Entries { get; } = new();
    public ObservableCollection<PyramidRowVm> PyramidRows { get; } = new();

    private MiracleOption? _selectedMiracleOption;
    public MiracleOption? SelectedMiracleOption
    {
        get => _selectedMiracleOption;
        set
        {
            if (!Set(ref _selectedMiracleOption, value)) return;
            Raise(nameof(CanAddSelected));
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value ?? string.Empty);
    }

    public ObservableCollection<string> SphereFilterOptions { get; } = new();
    public ObservableCollection<string> SelectedSphereFilters { get; } = new();
    public ObservableCollection<string> AdvancedFilterOptions { get; } = new() { "Advanced", "Handbook" };
    public ObservableCollection<string> SelectedAdvancedFilters { get; } = new();

    private Dictionary<string, MiracleOption> _filteredOptions = new();
    public Dictionary<string, MiracleOption> FilteredOptions
    {
        get => _filteredOptions;
        private set => Set(ref _filteredOptions, value);
    }

    public ICommand AddSelectedCommand { get; }
    public ICommand RemoveEntryCommand { get; }

    public string ListTitle => "Evil Stairway";

    public bool HasChurchList => _churchMiracleNames is { Count: > 0 };

    public string ChurchName { get; }

    public string ListSourceLabel => HasChurchList
        ? $"Using {ChurchName} church list"
        : "Unaligned: standard evil/neutral miracles only.";

    public bool ShowAdvancedFilters => HasChurchList;

    private int _totalSpiritCost;
    public int TotalSpiritCost { get => _totalSpiritCost; private set => Set(ref _totalSpiritCost, value); }

    public int RemainingSpiritCost => Math.Max(0, MaxSpiritTotal - TotalSpiritCost);

    public string SpiritProgressText => $"Spirits used: {TotalSpiritCost}/{MaxSpiritTotal}";

    public string RemainingSpiritText => $"Remaining: {RemainingSpiritCost}";

    private int _causingSpiritCost;
    public int CausingSpiritCost { get => _causingSpiritCost; private set => Set(ref _causingSpiritCost, value); }

    private bool _requiresCausingQuota;
    public bool RequiresCausingQuota
    {
        get => _requiresCausingQuota;
        private set
        {
            if (!Set(ref _requiresCausingQuota, value)) return;
            Raise(nameof(ShowCausingRequirement));
            Raise(nameof(CausingProgressText));
        }
    }

    public bool ShowCausingRequirement => RequiresCausingQuota;

    public string CausingProgressText => RequiresCausingQuota
        ? $"Causing spirits: {CausingSpiritCost}/{RequiredCausingSpirits}"
        : string.Empty;

    private bool _hasValidationError;
    public bool HasValidationError { get => _hasValidationError; private set => Set(ref _hasValidationError, value); }

    private string _validationMessage = string.Empty;
    public string ValidationMessage { get => _validationMessage; private set => Set(ref _validationMessage, value); }

    public bool CanAddSelected => SelectedMiracleOption.HasValue && CanAddOption(SelectedMiracleOption.Value);

    public EvilStairwayVm(
        MiracleListDraft draft,
        IReadOnlyList<MiracleService.MiracRaw> allMiracles,
        IReadOnlyDictionary<string, IReadOnlyList<string>> preReqs,
        string? churchName,
        HashSet<string>? churchMiracleNames,
        Action onValidationChanged)
    {
        Draft = draft;
        _allMiracles = allMiracles ?? Array.Empty<MiracleService.MiracRaw>();
        _preReqs = preReqs ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        _churchMiracleNames = churchMiracleNames;
        _sphereLookup = BuildSphereLookup();
        _onValidationChanged = onValidationChanged;

        ChurchName = string.IsNullOrWhiteSpace(churchName) ? "Church" : churchName.Trim();

        foreach (var sphere in Enum.GetValues<SpiritualSpheres>())
        {
            var label = StripMajorMinorPrefix(EnumDisplayFormatter.Format(sphere));
            if (IsUniversalSphere(label))
                continue;
            SphereFilterOptions.Add(label);
        }

        AddSelectedCommand = new Command(AddSelectedMiracle);
        RemoveEntryCommand = new Command<MiracleEntryVm>(RemoveEntry);

        SelectedSphereFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        SelectedAdvancedFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();

        LoadEntriesFromDraft();
        RequiresCausingQuota = HasChurchList && IsChurchCausingHeavy();
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void LoadEntriesFromDraft()
    {
        Entries.Clear();
        foreach (var entry in Draft.Entries ?? new List<MiracleListEntryDraft>())
        {
            var vm = new MiracleEntryVm(entry, OnEntryChanged);
            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                var sphereLabel = MapSphereLabel(entry.Sphere);
                vm.SelectedMiracle = new MiracleOption(
                    entry.Name,
                    entry.Power,
                    entry.Alignment ?? string.Empty,
                    sphereLabel,
                    entry.IsAdvanced);
            }
            Entries.Add(vm);
        }

        RebuildPyramid();
    }

    private void AddSelectedMiracle()
    {
        if (!CanAddSelected || SelectedMiracleOption == null)
            return;

        var draft = new MiracleListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new MiracleEntryVm(draft, OnEntryChanged);
        vm.SelectedMiracle = SelectedMiracleOption.Value;
        Entries.Add(vm);

        SelectedMiracleOption = null;
        SearchText = string.Empty;
        UpdateFilteredOptions();
        UpdateValidation();
        RebuildPyramid();
    }

    private void RemoveEntry(MiracleEntryVm? entry)
    {
        if (entry == null) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        UpdateFilteredOptions();
        UpdateValidation();
        RebuildPyramid();
    }

    private void OnEntryChanged()
    {
        UpdateFilteredOptions();
        UpdateValidation();
        RebuildPyramid();
    }

    private void UpdateFilteredOptions()
    {
        var filtered = _allMiracles;

        if (HasChurchList)
            filtered = filtered.Where(m => _churchMiracleNames!.Contains(m.name ?? string.Empty)).ToList();
        else
            filtered = filtered.Where(m => !m.isAdvanced).ToList();

        filtered = filtered
            .Where(m => IsAllowedAlignment(m.alignment))
            .ToList();

        if (SelectedSphereFilters.Count > 0)
        {
            var sphereSet = SelectedSphereFilters
                .Select(NormalizeToken)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            filtered = filtered.Where(m =>
            {
                var label = MapSphereLabel(m.sphere);
                if (IsUniversalSphere(label))
                    return true;
                return sphereSet.Contains(NormalizeToken(label));
            }).ToList();
        }

        if (HasChurchList)
        {
            var advanced = SelectedAdvancedFilters.Any(x => x.Equals("Advanced", StringComparison.OrdinalIgnoreCase));
            var handbook = SelectedAdvancedFilters.Any(x => x.Equals("Handbook", StringComparison.OrdinalIgnoreCase));
            if (advanced ^ handbook)
                filtered = filtered.Where(m => m.isAdvanced == advanced).ToList();
        }

        var dict = new Dictionary<string, MiracleOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var miracle in filtered)
        {
            if (string.IsNullOrWhiteSpace(miracle?.name))
                continue;

            var option = new MiracleOption(
                miracle.name ?? string.Empty,
                miracle.power,
                miracle.alignment ?? string.Empty,
                MapSphereLabel(miracle.sphere),
                miracle.isAdvanced);

            var label = $"{miracle.name} ({miracle.power})";
            if (!dict.ContainsKey(label))
                dict[label] = option;
        }

        FilteredOptions = dict;
    }

    private void UpdateValidation()
    {
        var messages = new List<string>();
        var missingPrereqs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invalidAligned = new List<string>();
        var invalidChurch = new List<string>();
        var invalidAdvanced = new List<string>();

        var totalCost = 0;
        var causingCost = 0;
        var powerCounts = new Dictionary<int, int>();

        var entries = Entries.Where(e => !string.IsNullOrWhiteSpace(e.Draft.Name)).ToList();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var name = entry.Draft.Name ?? string.Empty;
            var power = Math.Max(0, entry.Draft.Power);
            var alignment = entry.Draft.Alignment ?? string.Empty;
            var sphere = ResolveEntrySphere(entry);

            if (!IsAllowedAlignment(alignment))
                invalidAligned.Add(name);

            if (HasChurchList && !_churchMiracleNames!.Contains(name))
                invalidChurch.Add(name);

            if (!HasChurchList && entry.Draft.IsAdvanced)
                invalidAdvanced.Add(name);

            if (power > 0)
                powerCounts[power] = powerCounts.TryGetValue(power, out var c) ? c + 1 : 1;

            var cost = GetSpiritCost(power, entry.Draft.IsAdvanced);
            totalCost += cost;

            if (IsCausingSphere(sphere))
                causingCost += cost;

            if (_preReqs.TryGetValue(name, out var reqs))
            {
                foreach (var req in reqs)
                {
                    if (!entries.Any(e => string.Equals(e.Draft.Name, req, StringComparison.OrdinalIgnoreCase)))
                        missingPrereqs.Add(req);
                }
            }

            var row = GetRowForPosition(i + 1);
            if (power > row)
                messages.Add($"Row {row} cannot include P{power} miracles.");
        }

        foreach (var kvp in powerCounts)
        {
            if (kvp.Key <= 1) continue;
            var lowerCount = powerCounts.TryGetValue(kvp.Key - 1, out var lower) ? lower : 0;
            if (kvp.Value > lowerCount)
            {
                messages.Add($"Pyramid rule: P{kvp.Key} exceeds P{kvp.Key - 1} count.");
                break;
            }
        }

        if (totalCost > MaxSpiritTotal)
            messages.Add($"Exceeds {MaxSpiritTotal} spirit limit.");

        if (missingPrereqs.Count > 0)
            messages.Add($"Missing prerequisites: {string.Join(", ", missingPrereqs.Take(4))}{(missingPrereqs.Count > 4 ? "…" : string.Empty)}.");

        if (invalidAligned.Count > 0)
            messages.Add("List contains non-evil miracles.");

        if (invalidChurch.Count > 0)
            messages.Add("List contains miracles outside the church list.");

        if (invalidAdvanced.Count > 0)
            messages.Add("Unaligned champions cannot take advanced miracles.");

        if (RequiresCausingQuota)
        {
            var remaining = Math.Max(0, MaxSpiritTotal - totalCost);
            if (causingCost + remaining < RequiredCausingSpirits)
                messages.Add($"Requires at least {RequiredCausingSpirits} causing spirits.");
        }

        TotalSpiritCost = totalCost;
        CausingSpiritCost = causingCost;

        Raise(nameof(RemainingSpiritCost));
        Raise(nameof(SpiritProgressText));
        Raise(nameof(RemainingSpiritText));
        Raise(nameof(CausingProgressText));

        ValidationMessage = string.Join(" ", messages);
        HasValidationError = messages.Count > 0;

        Raise(nameof(CanAddSelected));
        _onValidationChanged();
    }

    private void RebuildPyramid()
    {
        PyramidRows.Clear();
        var entries = Entries.Where(e => !string.IsNullOrWhiteSpace(e.Draft.Name)).ToList();

        var index = 0;
        var row = 1;

        if (entries.Count == 0)
        {
            var slots = new List<PyramidSlotVm>
            {
                new(null, RemoveEntryCommand, isPeak: true)
            };
            PyramidRows.Add(new PyramidRowVm(row, slots));
            return;
        }

        while (index < entries.Count)
        {
            var slots = new List<PyramidSlotVm>();
            for (var col = 0; col < row; col++)
            {
                if (index < entries.Count)
                {
                    slots.Add(new PyramidSlotVm(entries[index], RemoveEntryCommand, row == 1 && col == 0));
                    index++;
                }
                else
                {
                    slots.Add(new PyramidSlotVm(null, RemoveEntryCommand, row == 1 && col == 0));
                }
            }

            PyramidRows.Add(new PyramidRowVm(row, slots));
            row++;
        }
    }

    private bool CanAddOption(MiracleOption option)
    {
        if (!IsAllowedAlignment(option.Alignment))
            return false;

        if (!HasChurchList && option.IsAdvanced)
            return false;

        if (HasChurchList && !_churchMiracleNames!.Contains(option.Name))
            return false;

        var cost = GetSpiritCost(option.Power, option.IsAdvanced);
        if (TotalSpiritCost + cost > MaxSpiritTotal)
            return false;

        var position = Entries.Count(e => !string.IsNullOrWhiteSpace(e.Draft.Name)) + 1;
        var row = GetRowForPosition(position);
        if (option.Power > row)
            return false;

        if (!HasPrereqs(option.Name))
            return false;

        if (ViolatesPyramidCounts(option.Power))
            return false;

        return true;
    }

    private bool HasPrereqs(string name)
    {
        if (!_preReqs.TryGetValue(name, out var reqs) || reqs.Count == 0)
            return true;

        foreach (var req in reqs)
        {
            if (!Entries.Any(e => string.Equals(e.Draft.Name, req, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    private bool ViolatesPyramidCounts(int power)
    {
        if (power <= 1)
            return false;

        var lowerCount = Entries.Count(e => e.Draft.Power == power - 1 && !string.IsNullOrWhiteSpace(e.Draft.Name));
        var currentCount = Entries.Count(e => e.Draft.Power == power && !string.IsNullOrWhiteSpace(e.Draft.Name));
        return currentCount + 1 > lowerCount;
    }

    private bool IsAllowedAlignment(string? alignment)
    {
        var token = NormalizeAlignment(alignment);
        return token is "evil" or "neutral";
    }

    private string ResolveEntrySphere(MiracleEntryVm entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Draft.Sphere))
            return entry.Draft.Sphere;

        var match = _allMiracles.FirstOrDefault(m => string.Equals(m.name, entry.Draft.Name, StringComparison.OrdinalIgnoreCase));
        return match == null ? string.Empty : MapSphereLabel(match.sphere);
    }

    private bool IsChurchCausingHeavy()
    {
        if (!HasChurchList)
            return false;

        var total = 0;
        var causing = 0;

        foreach (var name in _churchMiracleNames!)
        {
            var match = _allMiracles.FirstOrDefault(m => string.Equals(m.name, name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                continue;

            total++;
            var sphere = MapSphereLabel(match.sphere);
            if (IsCausingSphere(sphere))
                causing++;
        }

        return total > 0 && causing * 2 >= total;
    }

    private static int GetSpiritCost(int power, bool isAdvanced)
        => isAdvanced ? power * 2 : power;

    private static int GetRowForPosition(int position)
    {
        var row = 1;
        var total = 1;
        while (total < position)
        {
            row++;
            total += row;
        }

        return row;
    }

    private string MapSphereLabel(string raw)
    {
        var cleaned = StripMajorMinorPrefix(raw);
        var key = NormalizeToken(cleaned);
        if (_sphereLookup.TryGetValue(key, out var label))
            return label;

        return cleaned?.Trim() ?? string.Empty;
    }

    private static bool IsUniversalSphere(string? label)
        => NormalizeToken(label) == "universal";

    private static bool IsCausingSphere(string? label)
        => NormalizeToken(label) == "causing";

    private static string StripMajorMinorPrefix(string? raw)
    {
        var text = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.StartsWith("Major", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Minor", StringComparison.OrdinalIgnoreCase))
        {
            if (text.Length <= 5)
                return string.Empty;

            var trimmed = text.Substring(5).TrimStart(' ', ':', '-');
            return trimmed.Trim();
        }

        return text;
    }

    private static string NormalizeToken(string? value)
    {
        var text = value ?? string.Empty;
        var chars = text.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static string NormalizeAlignment(string? value)
    {
        var token = NormalizeToken(value);
        if (token.StartsWith("good", StringComparison.OrdinalIgnoreCase))
            return "good";
        if (token.StartsWith("evil", StringComparison.OrdinalIgnoreCase))
            return "evil";
        return "neutral";
    }

    private Dictionary<string, string> BuildSphereLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sphere in Enum.GetValues<SpiritualSpheres>())
        {
            var label = StripMajorMinorPrefix(EnumDisplayFormatter.Format(sphere));
            var key = NormalizeToken(label);
            if (!lookup.ContainsKey(key))
                lookup[key] = label;
        }
        return lookup;
    }
}

public sealed class PyramidRowVm
{
    public int RowIndex { get; }
    public ObservableCollection<PyramidSlotVm> Slots { get; }

    public PyramidRowVm(int rowIndex, IEnumerable<PyramidSlotVm> slots)
    {
        RowIndex = rowIndex;
        Slots = new ObservableCollection<PyramidSlotVm>(slots);
    }
}

public sealed class PyramidSlotVm
{
    public MiracleEntryVm? Entry { get; }
    public ICommand? RemoveCommand { get; }
    public bool IsPeak { get; }

    public bool HasEntry => Entry != null;
    public bool IsPlaceholder => Entry == null;

    public PyramidSlotVm(MiracleEntryVm? entry, ICommand? removeCommand, bool isPeak)
    {
        Entry = entry;
        RemoveCommand = removeCommand;
        IsPeak = isPeak;
    }

    public object? RemoveCommandParameter => Entry;

    public string Name => Entry?.Draft.Name ?? string.Empty;

    public string PowerLabel => Entry == null ? string.Empty : $"P{Entry.Draft.Power}";

    public bool IsAdvanced => Entry?.Draft.IsAdvanced ?? false;
}
