using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Services;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Search;

public sealed class GlobalSearchVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private const int MaxVisibleResults = 300;

    private static readonly IReadOnlyDictionary<string, GlobalSearchKind?> FilterMap =
        new Dictionary<string, GlobalSearchKind?>(StringComparer.OrdinalIgnoreCase)
        {
            ["All"] = null,
            ["Abilities"] = GlobalSearchKind.Ability,
            ["Spells"] = GlobalSearchKind.Spell,
            ["Miracles"] = GlobalSearchKind.Miracle,
            ["Evocations"] = GlobalSearchKind.Evocation
        };

    private readonly List<GlobalSearchResultVm> _allResults = new();
    private bool _isLoaded;

    public ObservableCollection<string> Filters { get; } = new()
    {
        "All",
        "Abilities",
        "Spells",
        "Miracles",
        "Evocations"
    };

    public ObservableCollection<GlobalSearchResultVm> FilteredResults { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? string.Empty))
                return;

            ApplyFilters();
        }
    }

    private string _selectedFilter = "All";
    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            var resolved = string.IsNullOrWhiteSpace(value) ? "All" : value.Trim();
            if (!Set(ref _selectedFilter, resolved))
                return;

            ApplyFilters();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!Set(ref _isLoading, value))
                return;

            Raise(nameof(HasNoResults));
            Raise(nameof(EmptyStateText));
        }
    }

    public bool HasNoResults => !IsLoading && FilteredResults.Count == 0;

    public string EmptyStateText
    {
        get
        {
            if (IsLoading)
                return "Loading search data...";

            if (string.IsNullOrWhiteSpace(SearchText))
                return "Type to search spells, miracles, evocations, and abilities.";

            return "No matches found.";
        }
    }

    public ICommand SelectFilterCommand { get; }

    public GlobalSearchVm()
    {
        SelectFilterCommand = new Command<string>(filter => SelectedFilter = filter ?? "All");
        FilteredResults.CollectionChanged += (_, __) =>
        {
            Raise(nameof(HasNoResults));
            Raise(nameof(EmptyStateText));
        };
    }

    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded)
        {
            ApplyFilters();
            return;
        }

        IsLoading = true;
        try
        {
            var evolutionAbilities = await SafeLoadAsync(EvolutionService.GetAllAbilitiesAsync);
            var manuAbilities = await SafeLoadAsync(ManuAbilityService.GetAllAsync);
            var spells = await SafeLoadAsync(async () =>
            {
                var list = await SpellService.GetAllAsync();
                return (IReadOnlyList<SpellService.SpellRaw>)list;
            });
            var miracles = await SafeLoadAsync(MiracleService.GetAllAsync);
            var evocations = await SafeLoadAsync(DruidEvocationService.GetAllAsync);

            BuildUnifiedResults(evolutionAbilities, manuAbilities, spells, miracles, evocations);
            _isLoaded = true;
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildUnifiedResults(
        IReadOnlyList<EvolutionService.AbilityResult> evolutionAbilities,
        IReadOnlyList<ManuAbilityService.ManuAbilityEntry> manuAbilities,
        IReadOnlyList<SpellService.SpellRaw> spells,
        IReadOnlyList<MiracleService.MiracRaw> miracles,
        IReadOnlyList<DruidEvocationService.EvocRaw> evocations)
    {
        _allResults.Clear();

        var abilityMap = new Dictionary<string, GlobalSearchResultVm>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in manuAbilities)
        {
            var key = BuildAbilityKey(ability.name, ability.table);
            abilityMap[key] = CreateAbilityResult(
                name: ability.name,
                table: ability.table,
                cost: ability.cost,
                availability: ability.availability,
                description: ability.description);
        }

        foreach (var ability in evolutionAbilities)
        {
            var key = BuildAbilityKey(ability.Index, ability.Table);
            if (abilityMap.ContainsKey(key))
                continue;

            abilityMap[key] = CreateAbilityResult(
                name: ability.Index,
                table: ability.Table,
                cost: ability.Cost,
                availability: ability.Available,
                description: ability.Description);
        }

        _allResults.AddRange(abilityMap.Values);

        _allResults.AddRange(
            spells
                .Where(s => !string.IsNullOrWhiteSpace(s.name))
                .Select(CreateSpellResult));

        _allResults.AddRange(
            miracles
                .Where(m => !string.IsNullOrWhiteSpace(m.name))
                .Select(CreateMiracleResult));

        _allResults.AddRange(
            evocations
                .Where(e => !string.IsNullOrWhiteSpace(e.name))
                .Select(CreateEvocationResult));
    }

    private void ApplyFilters()
    {
        var query = (_searchText ?? string.Empty).Trim();
        var normalized = query.ToLowerInvariant();
        var selectedKind = ResolveFilterKind(_selectedFilter);

        IEnumerable<GlobalSearchResultVm> results = _allResults;

        if (selectedKind.HasValue)
            results = results.Where(r => r.Kind == selectedKind.Value);

        if (normalized.Length > 0)
            results = results.Where(r => r.SearchBlob.Contains(normalized, StringComparison.Ordinal));

        var ordered = results
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Kind)
            .Take(MaxVisibleResults)
            .ToList();

        FilteredResults.Clear();
        foreach (var item in ordered)
            FilteredResults.Add(item);
    }

    private static GlobalSearchKind? ResolveFilterKind(string? filter)
    {
        if (filter == null)
            return null;

        return FilterMap.TryGetValue(filter, out var kind) ? kind : null;
    }

    private static async Task<IReadOnlyList<T>> SafeLoadAsync<T>(Func<Task<IReadOnlyList<T>>> loader)
    {
        try
        {
            return await loader();
        }
        catch
        {
            return Array.Empty<T>();
        }
    }

    private static string BuildAbilityKey(string? name, int table)
        => $"{(name ?? string.Empty).Trim().ToLowerInvariant()}|{table}";

    private static GlobalSearchResultVm CreateAbilityResult(string? name, int table, int cost, string? availability, string? description)
    {
        var title = (name ?? string.Empty).Trim();
        var avail = (availability ?? string.Empty).Trim();
        var meta = avail.Length > 0
            ? $"Ability · Table {table} · Cost {cost} · {avail}"
            : $"Ability · Table {table} · Cost {cost}";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Ability,
            Name: title,
            IconGlyph: "\uf013",
            MetaText: meta,
            DescriptionText: (description ?? string.Empty).Trim(),
            Spell: null,
            Miracle: null);
    }

    private static GlobalSearchResultVm CreateSpellResult(SpellService.SpellRaw spell)
    {
        var colour = (spell.colour ?? string.Empty).Trim();
        var meta = colour.Length > 0
            ? $"Spell · Lvl {Math.Max(0, spell.level)} · {colour}"
            : $"Spell · Lvl {Math.Max(0, spell.level)}";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Spell,
            Name: (spell.name ?? string.Empty).Trim(),
            IconGlyph: "\uf0e7",
            MetaText: meta,
            DescriptionText: (spell.description ?? string.Empty).Trim(),
            Spell: spell,
            Miracle: null);
    }

    private static GlobalSearchResultVm CreateMiracleResult(MiracleService.MiracRaw miracle)
    {
        var alignment = (miracle.alignment ?? string.Empty).Trim();
        var sphere = (miracle.sphere ?? string.Empty).Trim();
        var tags = new List<string> { $"P{Math.Max(0, miracle.power)}" };
        if (alignment.Length > 0) tags.Add(alignment);
        if (sphere.Length > 0) tags.Add(sphere);

        var meta = tags.Count > 0
            ? $"Miracle · {string.Join(" · ", tags)}"
            : "Miracle";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Miracle,
            Name: (miracle.name ?? string.Empty).Trim(),
            IconGlyph: "\uf005",
            MetaText: meta,
            DescriptionText: (miracle.description ?? string.Empty).Trim(),
            Spell: null,
            Miracle: miracle);
    }

    private static GlobalSearchResultVm CreateEvocationResult(DruidEvocationService.EvocRaw evocation)
    {
        var fields = (evocation.fields ?? new List<string>())
            .Select(f => (f ?? string.Empty).Trim())
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var meta = fields.Count > 0
            ? $"Evocation · P{Math.Max(0, evocation.power)} · {string.Join(", ", fields)}"
            : $"Evocation · P{Math.Max(0, evocation.power)}";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Evocation,
            Name: (evocation.name ?? string.Empty).Trim(),
            IconGlyph: "\uf06c",
            MetaText: meta,
            DescriptionText: string.Empty,
            Spell: null,
            Miracle: null);
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        Raise(name);
        return true;
    }
}

public enum GlobalSearchKind
{
    Ability,
    Spell,
    Miracle,
    Evocation
}

public sealed record GlobalSearchResultVm(
    GlobalSearchKind Kind,
    string Name,
    string IconGlyph,
    string MetaText,
    string DescriptionText,
    SpellService.SpellRaw? Spell,
    MiracleService.MiracRaw? Miracle)
{
    public bool CanOpenDetails => Spell != null || Miracle != null;

    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);

    public string SearchBlob
        => $"{Name} {MetaText} {DescriptionText}".ToLowerInvariant();
}
