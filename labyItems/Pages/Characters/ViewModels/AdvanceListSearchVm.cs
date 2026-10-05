using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

public enum ListSearchKind
{
    Spell,
    Miracle,
    Evocation
}

public sealed record ListSearchBreakdownRowVm(string Label, string Value, Color Colour, double Weight);

public sealed class ListSearchItemVm : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isAvailable = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ListSearchItemVm(
        string key,
        string name,
        string metaText,
        string detailText,
        bool isAdvanced,
        object option,
        object? raw,
        bool isSelected)
    {
        Key = key;
        Name = name;
        MetaText = metaText;
        DetailText = detailText;
        IsAdvanced = isAdvanced;
        Option = option;
        Raw = raw;
        _isSelected = isSelected;
    }

    public string Key { get; }
    public string Name { get; }
    public string MetaText { get; }
    public string DetailText { get; }
    public bool IsAdvanced { get; }
    public object Option { get; }
    public object? Raw { get; }
    public string TierText => IsAdvanced ? "Advanced" : "Standard";
    public string AvailabilityText => IsAvailable ? "Available" : "Unavailable";

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (_isAvailable == value)
                return;

            _isAvailable = value;
            Raise();
            Raise(nameof(AvailabilityText));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            Raise();
            Raise(nameof(SelectionGlyph));
            Raise(nameof(SelectionColor));
        }
    }

    public string SelectionGlyph => IsSelected ? "\uF058" : "\uF111";

    public Color SelectionColor => IsSelected
        ? Color.FromArgb("#7F1D1D")
        : Color.FromArgb("#6B7280");

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class AdvanceListSearchVm : INotifyPropertyChanged, IDisposable
{
    private const int MaxVisibleResults = 100;

    private readonly List<ListSearchItemVm> _allItems;
    private readonly HashSet<string> _initialKeys;
    private readonly HashSet<string> _selectedKeys;
    private readonly HashSet<string> _activeCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeTiers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<ListSearchItemVm, HashSet<string>, bool> _matchesCategory;
    private readonly Func<IReadOnlyList<ListSearchItemVm>, Func<ListSearchItemVm, bool>>? _availabilityFactory;
    private readonly Func<IReadOnlyList<ListSearchItemVm>, IReadOnlyList<ListSearchBreakdownRowVm>> _breakdownBuilder;
    private readonly Func<IReadOnlyList<ListSearchItemVm>, Task<bool>> _commit;

    private string _searchText = string.Empty;
    private bool _isFilterModalOpen;
    private bool _pendingAvailableOnly;
    private bool _activeAvailableOnly;
    private bool _showSelectedOnly;
    private bool _isDisposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ListSearchItemVm> FilteredItems { get; } = new();
    public ObservableCollection<AdvanceAbilitySearchFilterOptionVm<string>> CategoryFilters { get; } = new();
    public ObservableCollection<AdvanceAbilitySearchFilterOptionVm<string>> TierFilters { get; } = new();
    public ObservableCollection<ListSearchBreakdownRowVm> Breakdown { get; } = new();
    public ObservableCollection<EvocationFieldSegmentVm> BreakdownSegments { get; } = new();
    public ICommand ToggleBreakdownLegendCommand { get; }

    private bool _showBreakdownLegend;
    public bool ShowBreakdownLegend
    {
        get => _showBreakdownLegend;
        private set => Set(ref _showBreakdownLegend, value);
    }

    public ICommand OpenFiltersCommand { get; }
    public ICommand CancelFiltersCommand { get; }
    public ICommand ApplyFiltersCommand { get; }
    public ICommand ToggleCategoryFilterCommand { get; }
    public ICommand ToggleTierFilterCommand { get; }
    public ICommand ToggleSelectItemCommand { get; }
    public ICommand ToggleSelectedOnlyCommand { get; }

    public string TitleText { get; }
    public string SubtitleText { get; }
    public string SearchPlaceholder { get; }
    public string CategoryTitle { get; }
    public string BreakdownTitle { get; }
    public string EmptyText { get; }
    public bool IsAvailableFilterEnabled { get; }
    public string AvailableFilterDescription { get; }
    public string ConfirmButtonText => "Next";

    private AdvanceListSearchVm(
        string titleText,
        string subtitleText,
        string searchPlaceholder,
        string categoryTitle,
        string breakdownTitle,
        string emptyText,
        string availableDescription,
        List<ListSearchItemVm> items,
        IEnumerable<string> categoryOptions,
        IEnumerable<string> initialCategories,
        Func<ListSearchItemVm, HashSet<string>, bool> matchesCategory,
        Func<IReadOnlyList<ListSearchItemVm>, Func<ListSearchItemVm, bool>>? availabilityFactory,
        Func<IReadOnlyList<ListSearchItemVm>, IReadOnlyList<ListSearchBreakdownRowVm>> breakdownBuilder,
        Func<IReadOnlyList<ListSearchItemVm>, Task<bool>> commit)
    {
        TitleText = titleText;
        SubtitleText = subtitleText;
        SearchPlaceholder = searchPlaceholder;
        CategoryTitle = categoryTitle;
        BreakdownTitle = breakdownTitle;
        EmptyText = emptyText;
        AvailableFilterDescription = availableDescription;
        _allItems = items;
        _matchesCategory = matchesCategory;
        _availabilityFactory = availabilityFactory;
        _breakdownBuilder = breakdownBuilder;
        _commit = commit;
        IsAvailableFilterEnabled = availabilityFactory != null;
        _activeAvailableOnly = IsAvailableFilterEnabled;
        _pendingAvailableOnly = _activeAvailableOnly;

        _selectedKeys = new HashSet<string>(
            _allItems.Where(i => i.IsSelected).Select(i => i.Key),
            StringComparer.OrdinalIgnoreCase);
        _initialKeys = new HashSet<string>(_selectedKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var item in _allItems)
            item.PropertyChanged += OnItemPropertyChanged;

        foreach (var option in categoryOptions)
            CategoryFilters.Add(new AdvanceAbilitySearchFilterOptionVm<string>(option, option));
        foreach (var option in new[] { "Standard", "Advanced" })
            TierFilters.Add(new AdvanceAbilitySearchFilterOptionVm<string>(option, option));

        foreach (var category in initialCategories)
            _activeCategories.Add(category);

        OpenFiltersCommand = new Command(OpenFilters);
        ToggleBreakdownLegendCommand = new Command(() => ShowBreakdownLegend = !ShowBreakdownLegend);
        CancelFiltersCommand = new Command(() => IsFilterModalOpen = false);
        ApplyFiltersCommand = new Command(ApplyFilters);
        ToggleCategoryFilterCommand = new Command<AdvanceAbilitySearchFilterOptionVm<string>>(ToggleFilter);
        ToggleTierFilterCommand = new Command<AdvanceAbilitySearchFilterOptionVm<string>>(ToggleFilter);
        ToggleSelectItemCommand = new Command<ListSearchItemVm>(item =>
        {
            if (item != null)
                item.IsSelected = !item.IsSelected;
        });
        ToggleSelectedOnlyCommand = new Command(ToggleSelectedOnly);

        RebuildBreakdown();
        Refilter();
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? string.Empty))
                return;

            Refilter();
        }
    }

    public bool IsFilterModalOpen
    {
        get => _isFilterModalOpen;
        private set => Set(ref _isFilterModalOpen, value);
    }

    public bool PendingAvailableOnly
    {
        get => _pendingAvailableOnly;
        set => Set(ref _pendingAvailableOnly, value);
    }

    public bool HasResults => FilteredItems.Count > 0;
    public int SelectedCount => _selectedKeys.Count;
    public bool HasSelectedItems => SelectedCount > 0;
    public bool HasBreakdown => Breakdown.Count > 0;

    public bool HasChanges => !_selectedKeys.SetEquals(_initialKeys);
    public string CancelButtonText => HasChanges ? "Cancel" : "Back";

    public string SelectedSummaryText => HasSelectedItems
        ? ShowSelectedOnly
            ? $"Selected: {SelectedCount} (showing selected)"
            : $"Selected: {SelectedCount}"
        : "Selected: 0";

    public bool ShowSelectedOnly
    {
        get => _showSelectedOnly;
        private set
        {
            if (!Set(ref _showSelectedOnly, value))
                return;

            Raise(nameof(SelectedSummaryText));
        }
    }

    public async Task<bool> CommitAsync()
    {
        var selected = _allItems.Where(i => _selectedKeys.Contains(i.Key)).ToList();
        return await _commit(selected);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        foreach (var item in _allItems)
            item.PropertyChanged -= OnItemPropertyChanged;
    }

    public static AdvanceListSearchVm ForSpells(SpellListVm list)
    {
        var initial = list.Draft.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e?.Name))
            .GroupBy(e => SpellKey(e.Name, e.Level), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new SpellOption(g.First().Name, g.First().Level, g.First().Colour ?? string.Empty, g.First().IsAdvanced),
                StringComparer.OrdinalIgnoreCase);

        var items = new List<ListSearchItemVm>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spell in list.AllSpells
                     .Where(s => !string.IsNullOrWhiteSpace(s?.name))
                     .OrderBy(s => s.level)
                     .ThenBy(s => s.name, StringComparer.OrdinalIgnoreCase))
        {
            var key = SpellKey(spell.name, spell.level);
            if (!seen.Add(key))
                continue;

            var option = initial.TryGetValue(key, out var chosen)
                ? chosen
                : new SpellOption(spell.name, spell.level, spell.colour ?? string.Empty, spell.isAdvanced ?? false);
            items.Add(new ListSearchItemVm(
                key,
                spell.name,
                $"Level {spell.level}",
                spell.colour ?? string.Empty,
                spell.isAdvanced ?? false,
                option,
                spell,
                initial.ContainsKey(key)));
        }

        foreach (var pair in initial.Where(p => !seen.Contains(p.Key)))
        {
            items.Add(new ListSearchItemVm(
                pair.Key,
                pair.Value.Name,
                $"Level {pair.Value.Level}",
                pair.Value.Colour,
                pair.Value.IsAdvanced,
                pair.Value,
                null,
                true));
        }

        return new AdvanceListSearchVm(
            titleText: list.HeaderTitle,
            subtitleText: "Search spells and save selections",
            searchPlaceholder: "Search spells...",
            categoryTitle: "Colour",
            breakdownTitle: "Selected colours",
            emptyText: "No spells found for the current search/filter selection.",
            availableDescription: "Hide spells this character cannot take (for example opposite colours)",
            items: items,
            categoryOptions: list.ColourFilterOptions.ToList(),
            initialCategories: list.SelectedColourFilters.ToList(),
            matchesCategory: (item, categories) => categories.Any(c =>
                AdvanceCharacterVm.SpellMatchesWizardSelection(item.DetailText, c)),
            availabilityFactory: _ => item => item.Raw is not SpellService.SpellRaw spell || list.IsSpellAvailable(spell),
            breakdownBuilder: selected => BuildSpellBreakdown(selected),
            commit: async selected =>
            {
                var options = new List<SpellOption>();
                foreach (var item in selected)
                {
                    var option = (SpellOption)item.Option;
                    if (list.IsSpecialistList
                        && !WizardSpellRules.TryExtractSingleMagicColour(option.Colour, out _))
                    {
                        var chosen = await SpellListVm.PromptForSpecialistColourAsync(option.Name);
                        if (!chosen.HasValue)
                            return false;

                        option = option with { Colour = chosen.Value.ToString() };
                    }

                    options.Add(option);
                }

                list.ReplaceSelection(options);
                return true;
            });
    }

    public static AdvanceListSearchVm ForMiracles(MiracleListVm list)
    {
        var initial = list.Draft.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e?.Name))
            .GroupBy(e => PowerKey(e.Name, e.Power), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new MiracleOption(
                    g.First().Name,
                    g.First().Power,
                    g.First().Alignment ?? string.Empty,
                    list.MapSphereLabel(g.First().Sphere),
                    g.First().IsAdvanced),
                StringComparer.OrdinalIgnoreCase);

        var items = new List<ListSearchItemVm>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var miracle in list.AllMiracles
                     .Where(m => !string.IsNullOrWhiteSpace(m?.name))
                     .OrderBy(m => m.power)
                     .ThenBy(m => m.name, StringComparer.OrdinalIgnoreCase))
        {
            var key = PowerKey(miracle.name, miracle.power);
            if (!seen.Add(key))
                continue;

            var option = initial.TryGetValue(key, out var chosen)
                ? chosen
                : new MiracleOption(
                    miracle.name,
                    miracle.power,
                    miracle.alignment ?? string.Empty,
                    list.MapSphereLabel(miracle.sphere),
                    miracle.isAdvanced);
            items.Add(BuildMiracleItem(key, option, miracle, initial.ContainsKey(key)));
        }

        foreach (var pair in initial.Where(p => !seen.Contains(p.Key)))
            items.Add(BuildMiracleItem(pair.Key, pair.Value, null, true));

        return new AdvanceListSearchVm(
            titleText: list.HeaderTitle,
            subtitleText: "Search miracles and save selections",
            searchPlaceholder: "Search miracles...",
            categoryTitle: "Sphere",
            breakdownTitle: "Selected spheres",
            emptyText: "No miracles found for the current search/filter selection.",
            availableDescription: "Hide miracles that conflict with this character's alignment or the list's alignment",
            items: items,
            categoryOptions: list.SphereFilterOptions.ToList(),
            initialCategories: list.SelectedSphereFilters.ToList(),
            matchesCategory: (item, categories) =>
            {
                var sphere = ((MiracleOption)item.Option).Sphere;
                if (NormalizeToken(sphere) == "universal")
                    return true;

                return categories.Any(c => NormalizeToken(c) == NormalizeToken(sphere));
            },
            availabilityFactory: selected =>
            {
                var drafts = selected.Select(s =>
                {
                    var option = (MiracleOption)s.Option;
                    return new MiracleListEntryDraft
                    {
                        Name = option.Name,
                        Power = option.Power,
                        Alignment = option.Alignment,
                        Sphere = option.Sphere,
                        IsAdvanced = option.IsAdvanced
                    };
                }).ToList();
                var allowed = list.GetAllowedAlignments(drafts);
                return item => allowed.Contains(list.NormalizeAlignment(((MiracleOption)item.Option).Alignment));
            },
            breakdownBuilder: selected => BuildMiracleBreakdown(selected),
            commit: selected =>
            {
                list.ReplaceSelection(selected.Select(s => (MiracleOption)s.Option));
                return Task.FromResult(true);
            });
    }

    public static AdvanceListSearchVm ForEvocations(EvocationListVm list)
    {
        var lookup = list.AllEvocations
            .Where(e => !string.IsNullOrWhiteSpace(e?.name))
            .GroupBy(e => PowerKey(e.name, e.power), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var initial = list.Draft.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e?.Name))
            .GroupBy(e => PowerKey(e.Name, e.Power), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = new List<ListSearchItemVm>();
        foreach (var pair in lookup
                     .OrderBy(p => p.Value.power)
                     .ThenBy(p => p.Value.name, StringComparer.OrdinalIgnoreCase))
        {
            var ev = pair.Value;
            var fields = ev.fields ?? new List<string>();
            items.Add(new ListSearchItemVm(
                pair.Key,
                ev.name,
                $"{ev.power} EP",
                string.Join(", ", fields),
                ev.isAdvanced,
                new EvocationOption(ev.name, ev.power, ev.isAdvanced, fields),
                ev,
                initial.Contains(pair.Key)));
        }

        var fallbackSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in list.Draft.Entries.Where(e => !string.IsNullOrWhiteSpace(e?.Name)))
        {
            var key = PowerKey(entry.Name, entry.Power);
            if (lookup.ContainsKey(key) || !fallbackSeen.Add(key))
                continue;

            items.Add(new ListSearchItemVm(
                key,
                entry.Name,
                $"{entry.Power} EP",
                string.Empty,
                entry.IsAdvanced,
                new EvocationOption(entry.Name, entry.Power, entry.IsAdvanced, Array.Empty<string>()),
                null,
                true));
        }

        return new AdvanceListSearchVm(
            titleText: list.HeaderTitle,
            subtitleText: "Search evocations and save selections",
            searchPlaceholder: "Search evocations...",
            categoryTitle: "Field",
            breakdownTitle: "Selected fields",
            emptyText: "No evocations found for the current search/filter selection.",
            availableDescription: "Availability rules are not applied to evocations",
            items: items,
            categoryOptions: list.FieldFilterOptions.ToList(),
            initialCategories: list.SelectedFieldFilters.ToList(),
            matchesCategory: (item, categories) =>
                EvocationFieldCatalog.MatchesAnySelectedField(((EvocationOption)item.Option).Fields, categories),
            availabilityFactory: null,
            breakdownBuilder: selected => BuildEvocationBreakdown(selected),
            commit: selected =>
            {
                list.ReplaceSelection(selected.Select(s => (EvocationOption)s.Option));
                return Task.FromResult(true);
            });
    }

    private static ListSearchItemVm BuildMiracleItem(
        string key,
        MiracleOption option,
        MiracleService.MiracRaw? raw,
        bool isSelected)
        => new(
            key,
            option.Name,
            $"Power {option.Power}",
            string.Join(" · ", new[] { option.Alignment, option.Sphere }.Where(s => !string.IsNullOrWhiteSpace(s))),
            option.IsAdvanced,
            option,
            raw,
            isSelected);

    private static IReadOnlyList<ListSearchBreakdownRowVm> BuildSpellBreakdown(IReadOnlyList<ListSearchItemVm> selected)
    {
        var rows = new Dictionary<string, (int Count, Color Colour)>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in selected)
        {
            var colourText = ((SpellOption)item.Option).Colour;
            string label;
            var colour = Colors.Gray;
            if (WizardSpellRules.TryExtractSingleMagicColour(colourText, out var magicColour))
            {
                label = magicColour.ToString();
                colour = magicColour.ToColour();
            }
            else
            {
                label = string.IsNullOrWhiteSpace(colourText) ? "Unknown" : colourText.Trim();
            }

            rows[label] = (rows.GetValueOrDefault(label).Count + 1, colour);
        }

        return rows
            .OrderByDescending(r => r.Value.Count)
            .ThenBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ListSearchBreakdownRowVm(r.Key, $"{r.Value.Count} spell(s)", r.Value.Colour, r.Value.Count))
            .ToList();
    }

    private static IReadOnlyList<ListSearchBreakdownRowVm> BuildMiracleBreakdown(IReadOnlyList<ListSearchItemVm> selected)
    {
        return selected
            .Select(s => (MiracleOption)s.Option)
            .GroupBy(o => string.IsNullOrWhiteSpace(o.Sphere) ? "Unknown" : o.Sphere, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Label = g.Key, Count = g.Count(), Power = g.Sum(o => o.Power) })
            .OrderByDescending(r => r.Power)
            .ThenBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ListSearchBreakdownRowVm(
                r.Label,
                $"{r.Count} miracle(s), {r.Power} power",
                Color.FromArgb("#6B7280"),
                Math.Max(1, r.Power)))
            .ToList();
    }

    private static IReadOnlyList<ListSearchBreakdownRowVm> BuildEvocationBreakdown(IReadOnlyList<ListSearchItemVm> selected)
    {
        var totals = new Dictionary<string, (int Power, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in selected.Select(s => (EvocationOption)s.Option))
        {
            foreach (var field in EvocationFieldCatalog.ResolveFields(option.Fields))
            {
                var current = totals.GetValueOrDefault(field.Key);
                totals[field.Key] = (current.Power + Math.Max(0, option.Power), current.Count + 1);
            }
        }

        return EvocationFieldCatalog.Definitions
            .Where(def => totals.ContainsKey(def.Key))
            .Select(def => new ListSearchBreakdownRowVm(
                def.DisplayName,
                $"{totals[def.Key].Count} evocation(s), {totals[def.Key].Power} EP",
                def.Colour,
                Math.Max(1, totals[def.Key].Power)))
            .ToList();
    }

    private static string SpellKey(string name, int level) => $"{name.Trim()}|{level}";

    private static string PowerKey(string name, int power) => $"{name.Trim()}|{power}";

    private static string NormalizeToken(string? value)
        => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private void OpenFilters()
    {
        PendingAvailableOnly = _activeAvailableOnly;

        foreach (var filter in CategoryFilters)
            filter.IsSelected = _activeCategories.Contains(filter.Value);

        foreach (var filter in TierFilters)
            filter.IsSelected = _activeTiers.Contains(filter.Value);

        RebuildBreakdown();
        IsFilterModalOpen = true;
    }

    private void ApplyFilters()
    {
        _activeAvailableOnly = IsAvailableFilterEnabled && PendingAvailableOnly;

        _activeCategories.Clear();
        foreach (var filter in CategoryFilters.Where(f => f.IsSelected))
            _activeCategories.Add(filter.Value);

        _activeTiers.Clear();
        foreach (var filter in TierFilters.Where(f => f.IsSelected))
            _activeTiers.Add(filter.Value);

        IsFilterModalOpen = false;
        Refilter();
    }

    private static void ToggleFilter(AdvanceAbilitySearchFilterOptionVm<string>? filter)
    {
        if (filter != null)
            filter.IsSelected = !filter.IsSelected;
    }

    private void ToggleSelectedOnly()
    {
        if (SelectedCount <= 0)
        {
            ShowSelectedOnly = false;
        }
        else
        {
            ShowSelectedOnly = !ShowSelectedOnly;
        }

        Refilter();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ListSearchItemVm.IsSelected) || sender is not ListSearchItemVm item)
            return;

        if (item.IsSelected)
            _selectedKeys.Add(item.Key);
        else
            _selectedKeys.Remove(item.Key);

        Raise(nameof(SelectedCount));
        Raise(nameof(HasSelectedItems));
        Raise(nameof(SelectedSummaryText));
        Raise(nameof(HasChanges));
        Raise(nameof(CancelButtonText));
        RebuildBreakdown();

        if (SelectedCount == 0 && ShowSelectedOnly)
            ShowSelectedOnly = false;

        if (ShowSelectedOnly)
            Refilter();
        else
            UpdateAvailability();
    }

    private void UpdateAvailability()
    {
        if (_availabilityFactory == null)
            return;

        var isAvailable = _availabilityFactory(_allItems.Where(i => _selectedKeys.Contains(i.Key)).ToList());
        foreach (var item in _allItems)
            item.IsAvailable = isAvailable(item);
    }

    private void RebuildBreakdown()
    {
        Breakdown.Clear();
        BreakdownSegments.Clear();
        var selected = _allItems.Where(i => _selectedKeys.Contains(i.Key)).ToList();
        foreach (var row in _breakdownBuilder(selected))
        {
            Breakdown.Add(row);
            BreakdownSegments.Add(new EvocationFieldSegmentVm(row.Weight, row.Colour));
        }

        Raise(nameof(HasBreakdown));
    }

    private void Refilter()
    {
        if (_isDisposed)
            return;

        var query = SearchText.Trim();
        var standard = _activeTiers.Contains("Standard");
        var advanced = _activeTiers.Contains("Advanced");
        var filterTier = standard ^ advanced;

        var selectedItems = _allItems.Where(i => _selectedKeys.Contains(i.Key)).ToList();
        var isAvailable = _availabilityFactory?.Invoke(selectedItems);

        var matches = new List<ListSearchItemVm>();
        foreach (var item in _allItems)
        {
            if (isAvailable != null)
                item.IsAvailable = isAvailable(item);

            if (query.Length > 0 && !item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ShowSelectedOnly)
            {
                if (_selectedKeys.Contains(item.Key))
                    matches.Add(item);
                continue;
            }

            if (_activeCategories.Count > 0 && !_matchesCategory(item, _activeCategories))
                continue;

            if (filterTier && ((standard && item.IsAdvanced) || (advanced && !item.IsAdvanced)))
                continue;

            if (_activeAvailableOnly && !item.IsAvailable)
                continue;

            matches.Add(item);
        }

        IEnumerable<ListSearchItemVm> ordered = matches;
        if (query.Length > 0)
        {
            ordered = matches
                .OrderByDescending(i => i.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => i.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredItems.Clear();
        foreach (var item in ordered.Take(MaxVisibleResults))
            FilteredItems.Add(item);

        Raise(nameof(HasResults));
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        Raise(propertyName);
        return true;
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
