using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Models;
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
    public ObservableCollection<EvilStairwayRowVm> Rows { get; } = new();

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

        RebuildRows();
    }

    private void AddSelectedMiracle()
    {
        if (!CanAddSelected || SelectedMiracleOption == null)
            return;

        AddMiracle(SelectedMiracleOption.Value);

        SelectedMiracleOption = null;
        UpdateFilteredOptions();
        UpdateValidation();
        RebuildRows();
    }

    private void AddMiracle(MiracleOption option)
    {
        var draft = new MiracleListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new MiracleEntryVm(draft, OnEntryChanged);
        vm.SelectedMiracle = option;
        Entries.Add(vm);
    }

    private void AddMiracleFromRow(MiracleOption option)
    {
        if (!CanAddOption(option))
            return;

        AddMiracle(option);
        UpdateFilteredOptions();
        UpdateValidation();
        RebuildRows();
    }

    private void RemoveEntry(MiracleEntryVm? entry)
    {
        if (entry == null) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        UpdateFilteredOptions();
        UpdateValidation();
        RebuildRows();
    }

    private void RemoveOneByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var entry = Entries.LastOrDefault(e =>
            string.Equals(e.Draft.Name, name, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
            return;

        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        UpdateFilteredOptions();
        UpdateValidation();
        RebuildRows();
    }

    private void OnEntryChanged()
    {
        UpdateFilteredOptions();
        UpdateValidation();
        RebuildRows();
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

        var activeTree = GetActiveTree();
        if (activeTree != null)
        {
            var treeNames = activeTree.AllNames;
            filtered = filtered
                .Where(m =>
                    treeNames.Contains(m.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    || IsUniversalNonGoodly(m))
                .ToList();
        }

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

        }

        ApplyTreeValidation(entries, messages);

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

    private void RebuildRows()
    {
        Rows.Clear();

        var entries = Entries.Where(e => !string.IsNullOrWhiteSpace(e.Draft.Name)).ToList();
        if (entries.Count == 0)
            return;

        var grouped = entries
            .GroupBy(e => e.Draft.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Name = g.Key,
                Count = g.Count(),
                Sample = g.First()
            })
            .OrderBy(g => g.Sample.Draft.Power)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var running = 0;
        foreach (var group in grouped)
        {
            var option = new MiracleOption(
                group.Sample.Draft.Name,
                group.Sample.Draft.Power,
                group.Sample.Draft.Alignment,
                group.Sample.Draft.Sphere,
                group.Sample.Draft.IsAdvanced);

            var costPer = GetSpiritCost(option.Power, option.IsAdvanced);
            var rowTotal = costPer * group.Count;
            running += rowTotal;

            var canIncrease = CanAddOption(option);
            Rows.Add(new EvilStairwayRowVm(
                option,
                group.Count,
                costPer,
                running,
                canIncrease,
                () => AddMiracleFromRow(option),
                () => RemoveOneByName(option.Name)));
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

        var activeTree = GetActiveTree();
        if (activeTree != null && !activeTree.ContainsName(option.Name) && !IsUniversalNonGoodly(option))
            return false;

        var cost = GetSpiritCost(option.Power, option.IsAdvanced);
        if (TotalSpiritCost + cost > MaxSpiritTotal)
            return false;

        if (!HasPrereqs(option.Name))
            return false;

        if (ViolatesTreeCounts(option.Name))
            return false;

        return true;
    }

    private bool HasPrereqs(string name)
    {
        if (!_preReqs.TryGetValue(name, out var reqs) || reqs.Count == 0)
            return HasTreePrereqs(name);

        foreach (var req in reqs)
        {
            if (!Entries.Any(e => string.Equals(e.Draft.Name, req, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return HasTreePrereqs(name);
    }

    private void ApplyTreeValidation(List<MiracleEntryVm> entries, List<string> messages)
    {
        var tree = GetActiveTree();
        if (tree == null)
            return;

        for (var i = 0; i < tree.Nodes.Count; i++)
        {
            var node = tree.Nodes[i];
            var prevIndex = tree.GetPreviousRequiredIndex(i);
            if (prevIndex < 0)
                continue;

            var prev = tree.Nodes[prevIndex];
            var count = CountEntriesForNode(node);
            var prevCount = CountEntriesForNode(prev);

            if (count > prevCount)
                messages.Add($"Tree rule: {node.DisplayName} exceeds {prev.DisplayName} count.");

            if (count > 0 && prevCount == 0)
                messages.Add($"Missing prerequisite: {prev.DisplayName} required for {node.DisplayName}.");
        }
    }

    private int CountEntriesForNode(EvilStairwayNode node)
    {
        return Entries.Count(e =>
            !string.IsNullOrWhiteSpace(e.Draft.Name)
            && node.Matches(e.Draft.Name));
    }

    private EvilStairwayTree? GetActiveTree()
    {
        var first = Draft.Entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Name));
        if (first == null)
            return null;

        return EvilStairwayTrees.TreeLookup.TryGetValue(first.Name, out var tree) ? tree : null;
    }

    private bool HasTreePrereqs(string name)
    {
        var tree = GetActiveTree();
        if (tree == null)
            return true;

        var nodeIndex = tree.FindNodeIndex(name);
        if (nodeIndex < 0)
            return true;

        var prevIndex = tree.GetPreviousRequiredIndex(nodeIndex);
        if (prevIndex < 0)
            return true;

        var prevCount = CountEntriesForNode(tree.Nodes[prevIndex]);
        return prevCount > 0;
    }

    private bool ViolatesTreeCounts(string name)
    {
        var tree = GetActiveTree();
        if (tree == null)
            return false;

        var nodeIndex = tree.FindNodeIndex(name);
        if (nodeIndex < 0)
            return false;

        var prevIndex = tree.GetPreviousRequiredIndex(nodeIndex);
        if (prevIndex < 0)
            return false;

        var currentCount = CountEntriesForNode(tree.Nodes[nodeIndex]);
        var prevCount = CountEntriesForNode(tree.Nodes[prevIndex]);
        return currentCount + 1 > prevCount;
    }

    private bool IsAllowedAlignment(string? alignment)
    {
        var token = NormalizeAlignment(alignment);
        return token is "evil" or "neutral";
    }

    private bool IsUniversalNonGoodly(MiracleService.MiracRaw miracle)
    {
        var sphere = MapSphereLabel(miracle.sphere);
        return IsUniversalSphere(sphere) && IsAllowedAlignment(miracle.alignment);
    }

    private bool IsUniversalNonGoodly(MiracleOption option)
    {
        var sphere = MapSphereLabel(option.Sphere);
        return IsUniversalSphere(sphere) && IsAllowedAlignment(option.Alignment);
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

public sealed class EvilStairwayRowVm
{
    public EvilStairwayRowVm(
        MiracleOption option,
        int count,
        int costPer,
        int runningTotal,
        bool canIncrease,
        Action onIncrease,
        Action onDecrease)
    {
        Option = option;
        Count = count;
        CostPer = costPer;
        RunningTotal = runningTotal;
        CanIncrease = canIncrease;
        IncreaseCommand = new Command(onIncrease);
        DecreaseCommand = new Command(onDecrease);
    }

    public MiracleOption Option { get; }
    public int Count { get; }
    public int CostPer { get; }
    public int RunningTotal { get; }
    public bool CanIncrease { get; }
    public bool CanDecrease => Count > 0;

    public string Name => Option.Name;
    public int Power => Option.Power;
    public string UsesLabel => Count.ToString();
    public string TotalLabel => RunningTotal.ToString();

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }
}
