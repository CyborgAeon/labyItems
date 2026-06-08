using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using labyItems.Controls;
using labyItems.Infrastructure;
using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Pages.Search;

public sealed class GlobalSearchVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private const int MaxVisibleResults = 300;
    private const string BackChipKey = "__back";
    private const string TierHandbook = "Handbook";
    private const string TierAdvanced = "Advanced";

    private static readonly IReadOnlyDictionary<string, GlobalSearchKind?> FilterMap =
        new Dictionary<string, GlobalSearchKind?>(StringComparer.OrdinalIgnoreCase)
        {
            ["All"] = null,
            ["Abilities"] = GlobalSearchKind.Ability,
            ["Spells"] = GlobalSearchKind.Spell,
            ["Miracles"] = GlobalSearchKind.Miracle,
            ["Evocations"] = GlobalSearchKind.Evocation
        };

    private static readonly IReadOnlyList<string> PrimaryFilterOrder =
    [
        "All",
        "Abilities",
        "Spells",
        "Miracles",
        "Evocations"
    ];

    private static readonly IReadOnlyList<FilterOption> SpellColourOptions = BuildSpellColourOptions();
    private static readonly IReadOnlyList<FilterOption> AbilityTableOptions = BuildAbilityTableOptions();
    private static readonly IReadOnlyList<FilterOption> MiracleSphereOptions = BuildMiracleSphereOptions();
    private static readonly IReadOnlyList<FilterOption> EvocationFieldOptions = BuildEvocationFieldOptions();
    private static readonly HashSet<string> EvocationFieldTokens = new(EvocationFieldOptions.Select(o => o.Token), StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> EvocationFieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forest"] = NormalizeToken(EvocationFields.Forests.ToString()),
        ["river"] = NormalizeToken(EvocationFields.Rivers.ToString()),
        ["mountain"] = NormalizeToken(EvocationFields.Mountains.ToString()),
        ["desert"] = NormalizeToken(EvocationFields.Deserts.ToString()),
        ["city"] = NormalizeToken(EvocationFields.Cities.ToString()),
        ["sky"] = NormalizeToken(EvocationFields.Skies.ToString()),
        ["keeperofthewind"] = NormalizeToken(EvocationFields.KeeperOfWinds.ToString())
    };

    private readonly OptimizedSearchService _optimizedSearchService = new();
    private readonly CancellationTokenSource _cts = new();
    private DebouncedAsyncAction? _searchDebounce;
    private readonly HashSet<string> _selectedSpellColourTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedSpellTierTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedAbilityTableTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedMiracleSphereTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedMiracleTierTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedEvocationFieldTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedEvocationTierTokens = new(StringComparer.OrdinalIgnoreCase);

    private bool _isLoaded;
    private string _selectedPrimaryFilter = "All";
    private SearchSecondaryFilterMode _secondaryFilterMode = SearchSecondaryFilterMode.None;

    public ObservableCollection<GlobalSearchFilterChipVm> ActiveFilterChips { get; } = new();

    private IReadOnlyList<GlobalSearchResultVm> _filteredResults = Array.Empty<GlobalSearchResultVm>();
    public IReadOnlyList<GlobalSearchResultVm> FilteredResults
    {
        get => _filteredResults;
        private set
        {
            _filteredResults = value ?? Array.Empty<GlobalSearchResultVm>();
            Raise();
            Raise(nameof(HasNoResults));
            Raise(nameof(EmptyStateText));
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? string.Empty))
                return;

            // Debounce search with 300ms delay to avoid excessive filtering
            _searchDebounce ??= new DebouncedAsyncAction(300, ApplyFiltersAsync);
            _searchDebounce.Trigger();
        }
    }

    public string SelectedPrimaryFilter => _selectedPrimaryFilter;

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

    public GlobalSearchVm()
    {
        _searchDebounce = new DebouncedAsyncAction(300, ApplyFiltersAsync);
        RebuildActiveFilterChips();
    }

    public GlobalSearchFilterTransition PreviewFilterTransition(GlobalSearchFilterChipVm? chip)
    {
        if (chip == null)
            return GlobalSearchFilterTransition.None;

        if (_secondaryFilterMode == SearchSecondaryFilterMode.None && TryResolveSecondaryMode(chip.Key, out _))
            return GlobalSearchFilterTransition.ToSecondary;

        if (_secondaryFilterMode != SearchSecondaryFilterMode.None && chip.IsBack)
            return GlobalSearchFilterTransition.ToPrimary;

        return GlobalSearchFilterTransition.None;
    }

    public void ApplyFilterChip(GlobalSearchFilterChipVm? chip)
    {
        if (chip == null)
            return;

        if (_secondaryFilterMode == SearchSecondaryFilterMode.None)
        {
            if (!FilterMap.ContainsKey(chip.Key))
                return;

            _selectedPrimaryFilter = chip.Key;
            Raise(nameof(SelectedPrimaryFilter));

            if (TryResolveSecondaryMode(chip.Key, out var mode))
                _secondaryFilterMode = mode;
            else
                _secondaryFilterMode = SearchSecondaryFilterMode.None;
        }
        else
        {
            if (chip.IsBack)
            {
                _secondaryFilterMode = SearchSecondaryFilterMode.None;
            }
            else
            {
                ToggleSecondarySelection(chip.Key);
            }
        }

        RebuildActiveFilterChips();
        ApplyFilters();
    }

    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded)
        {
            await ApplyFiltersAsync();
            return;
        }

        IsLoading = true;
        try
        {
            // No need to preload all results - OptimizedSearchService queries database directly
            _isLoaded = true;
            await ApplyFiltersAsync();
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Search data load failed", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilters()
    {
        if (!_isLoaded)
            return;

        // Use debounced filtering instead of immediate
        _searchDebounce?.Trigger();
    }

    private async Task ApplyFiltersAsync(CancellationToken cancellationToken = default)
    {
        if (!_isLoaded)
            return;

        var selectedKind = ResolveFilterKind(_selectedPrimaryFilter);
        var allFilters = BuildSelectedFiltersSet();

        try
        {
            // Use optimized database-level filtering instead of in-memory
            var result = selectedKind.HasValue
                ? await _optimizedSearchService.SearchByKindAsync(
                    selectedKind.Value,
                    _searchText,
                    allFilters,
                    pageNumber: 1,
                    pageSize: 300,   // Limit to first 300 visible results
                    cancellationToken: cancellationToken)
                : await _optimizedSearchService.SearchAllAsync(
                    _searchText,
                    filterKind: null,
                    pageNumber: 1,
                    pageSize: 300,
                    cancellationToken: cancellationToken);

            // Convert optimized results back to GlobalSearchResultVm format
            var converted = await Task.Run(
                () => ConvertOptimizedResults(result.Results),
                cancellationToken);
            
            FilteredResults = converted;
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled by new search, ignore
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("ApplyFiltersAsync", ex);
        }
    }

    /// <summary>
    /// Converts OptimizedSearchService results back to GlobalSearchResultVm format.
    /// This bridges the optimized database queries with the existing UI model.
    /// </summary>
    private List<GlobalSearchResultVm> ConvertOptimizedResults(IReadOnlyList<OptimizedSearchService.SearchResultDto> results)
    {
        var converted = new List<GlobalSearchResultVm>();

        foreach (var result in results)
        {
            var kind = (GlobalSearchKind)result.Kind;
            var vm = kind switch
            {
                GlobalSearchKind.Ability => CreateAbilityResultFromDto(result),
                GlobalSearchKind.Spell => CreateSpellResultFromDto(result),
                GlobalSearchKind.Miracle => CreateMiracleResultFromDto(result),
                GlobalSearchKind.Evocation => CreateEvocationResultFromDto(result),
                _ => null
            };

            if (vm != null)
                converted.Add(vm);
        }

        return converted;
    }

    private GlobalSearchResultVm CreateAbilityResultFromDto(OptimizedSearchService.SearchResultDto dto)
    {
        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Ability,
            Name: dto.Name,
            GroupText: dto.ExtraInfo ?? string.Empty,
            IconGlyph: "\uf013",
            MetaText: $"Ability · {dto.ExtraInfo}",
            DescriptionText: dto.Description,
            Ability: null, // Loaded lazily when the card is opened.
            Spell: null,
            Miracle: null,
            Evocation: null)
        {
            DetailKey = string.IsNullOrWhiteSpace(dto.LookupKey) ? dto.Name : dto.LookupKey,
            AbilityTable = dto.AbilityTable
        };
    }

    private GlobalSearchResultVm CreateSpellResultFromDto(OptimizedSearchService.SearchResultDto dto)
    {
        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Spell,
            Name: dto.Name,
            GroupText: dto.ExtraInfo ?? string.Empty,
            IconGlyph: "\uf518",
            MetaText: $"Spell · {dto.ExtraInfo}",
            DescriptionText: dto.Description,
            Ability: null,
            Spell: null,
            Miracle: null,
            Evocation: null)
        {
            DetailKey = string.IsNullOrWhiteSpace(dto.LookupKey) ? dto.Name : dto.LookupKey
        };
    }

    private GlobalSearchResultVm CreateMiracleResultFromDto(OptimizedSearchService.SearchResultDto dto)
    {
        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Miracle,
            Name: dto.Name,
            GroupText: dto.ExtraInfo ?? string.Empty,
            IconGlyph: "\uf005",
            MetaText: $"Miracle · {dto.ExtraInfo}",
            DescriptionText: dto.Description,
            Ability: null,
            Spell: null,
            Miracle: null,
            Evocation: null)
        {
            DetailKey = string.IsNullOrWhiteSpace(dto.LookupKey) ? dto.Name : dto.LookupKey
        };
    }

    private GlobalSearchResultVm CreateEvocationResultFromDto(OptimizedSearchService.SearchResultDto dto)
    {
        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Evocation,
            Name: dto.Name,
            GroupText: dto.ExtraInfo ?? string.Empty,
            IconGlyph: "\uf06c",
            MetaText: $"Evocation · {dto.ExtraInfo}",
            DescriptionText: dto.Description,
            Ability: null,
            Spell: null,
            Miracle: null,
            Evocation: null)
        {
            DetailKey = string.IsNullOrWhiteSpace(dto.LookupKey) ? dto.Name : dto.LookupKey
        };
    }

    private HashSet<string> BuildSelectedFiltersSet()
    {
        var filters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        filters.UnionWith(_selectedSpellColourTokens.Select(t => $"spell-colour:{t}"));
        filters.UnionWith(_selectedSpellTierTokens.Select(t => $"spell-tier:{t}"));
        filters.UnionWith(_selectedAbilityTableTokens.Select(t => $"ability-table:{t}"));
        filters.UnionWith(_selectedMiracleSphereTokens.Select(t => $"miracle-sphere:{t}"));
        filters.UnionWith(_selectedMiracleTierTokens.Select(t => $"miracle-tier:{t}"));
        filters.UnionWith(_selectedEvocationFieldTokens.Select(t => $"evocation-field:{t}"));
        filters.UnionWith(_selectedEvocationTierTokens.Select(t => $"evocation-tier:{t}"));

        return filters;
    }

    private void ToggleSecondarySelection(string key)
    {
        switch (_secondaryFilterMode)
        {
            case SearchSecondaryFilterMode.Spell:
                ToggleSelectionForMode(key, "spell-colour:", _selectedSpellColourTokens);
                ToggleSelectionForMode(key, "spell-tier:", _selectedSpellTierTokens);
                break;
            case SearchSecondaryFilterMode.Ability:
                ToggleSelectionForMode(key, "ability-table:", _selectedAbilityTableTokens);
                break;
            case SearchSecondaryFilterMode.Miracle:
                ToggleSelectionForMode(key, "miracle-sphere:", _selectedMiracleSphereTokens);
                ToggleSelectionForMode(key, "miracle-tier:", _selectedMiracleTierTokens);
                break;
            case SearchSecondaryFilterMode.Evocation:
                ToggleSelectionForMode(key, "evocation-field:", _selectedEvocationFieldTokens);
                ToggleSelectionForMode(key, "evocation-tier:", _selectedEvocationTierTokens);
                break;
        }
    }

    private static void ToggleSelectionForMode(string key, string prefix, HashSet<string> selected)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return;

        var token = key[prefix.Length..];
        if (token.Length == 0)
            return;

        if (!selected.Add(token))
            selected.Remove(token);
    }

    private void RebuildActiveFilterChips()
    {
        var desired = BuildDesiredFilterChips();
        if (CanPatchActiveFilterChips(desired))
        {
            for (var i = 0; i < desired.Count; i++)
                ActiveFilterChips[i].UpdateFrom(desired[i]);
            return;
        }

        ActiveFilterChips.Clear();
        foreach (var chip in desired)
            ActiveFilterChips.Add(chip);
    }

    private bool CanPatchActiveFilterChips(IReadOnlyList<GlobalSearchFilterChipVm> desired)
    {
        if (ActiveFilterChips.Count != desired.Count)
            return false;

        for (var i = 0; i < desired.Count; i++)
        {
            if (!string.Equals(ActiveFilterChips[i].Key, desired[i].Key, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private List<GlobalSearchFilterChipVm> BuildDesiredFilterChips()
    {
        var chips = new List<GlobalSearchFilterChipVm>();

        if (_secondaryFilterMode == SearchSecondaryFilterMode.None)
        {
            foreach (var filter in PrimaryFilterOrder)
            {
                chips.Add(new GlobalSearchFilterChipVm(
                    key: filter,
                    label: filter,
                    isSelected: string.Equals(_selectedPrimaryFilter, filter, StringComparison.OrdinalIgnoreCase),
                    isBack: false));
            }

            return chips;
        }

        chips.Add(new GlobalSearchFilterChipVm(BackChipKey, "◀", false, true));

        switch (_secondaryFilterMode)
        {
            case SearchSecondaryFilterMode.Spell:
                AddTierChips(chips, "spell-tier:", _selectedSpellTierTokens);
                AddOptionChips(chips, "spell-colour:", SpellColourOptions, _selectedSpellColourTokens);
                break;
            case SearchSecondaryFilterMode.Ability:
                AddOptionChips(chips, "ability-table:", AbilityTableOptions, _selectedAbilityTableTokens);
                break;
            case SearchSecondaryFilterMode.Miracle:
                AddTierChips(chips, "miracle-tier:", _selectedMiracleTierTokens);
                AddOptionChips(chips, "miracle-sphere:", MiracleSphereOptions, _selectedMiracleSphereTokens);
                break;
            case SearchSecondaryFilterMode.Evocation:
                AddTierChips(chips, "evocation-tier:", _selectedEvocationTierTokens);
                AddOptionChips(chips, "evocation-field:", EvocationFieldOptions, _selectedEvocationFieldTokens);
                break;
        }

        return chips;
    }

    private static void AddTierChips(List<GlobalSearchFilterChipVm> chips, string prefix, HashSet<string> selectedTokens)
    {
        var handbookToken = NormalizeToken(TierHandbook);
        var advancedToken = NormalizeToken(TierAdvanced);

        chips.Add(new GlobalSearchFilterChipVm($"{prefix}{handbookToken}", TierHandbook, selectedTokens.Contains(handbookToken), false));
        chips.Add(new GlobalSearchFilterChipVm($"{prefix}{advancedToken}", TierAdvanced, selectedTokens.Contains(advancedToken), false));
    }

    private static void AddOptionChips(
        List<GlobalSearchFilterChipVm> chips,
        string prefix,
        IReadOnlyList<FilterOption> options,
        HashSet<string> selectedTokens)
    {
        foreach (var option in options)
        {
            chips.Add(new GlobalSearchFilterChipVm(
                key: $"{prefix}{option.Token}",
                label: option.Label,
                isSelected: selectedTokens.Contains(option.Token),
                isBack: false));
        }
    }

    private static bool TryResolveSecondaryMode(string key, out SearchSecondaryFilterMode mode)
    {
        mode = key switch
        {
            "Abilities" => SearchSecondaryFilterMode.Ability,
            "Spells" => SearchSecondaryFilterMode.Spell,
            "Miracles" => SearchSecondaryFilterMode.Miracle,
            "Evocations" => SearchSecondaryFilterMode.Evocation,
            _ => SearchSecondaryFilterMode.None
        };

        return mode != SearchSecondaryFilterMode.None;
    }

    private static GlobalSearchKind? ResolveFilterKind(string? filter)
    {
        if (filter == null)
            return null;

        return FilterMap.TryGetValue(filter, out var kind) ? kind : null;
    }

    private static string NormalizeMiracleSphereToken(string? raw)
        => NormalizeToken(StripMajorMinorPrefix(raw));

    private static string StripMajorMinorPrefix(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        if (text.StartsWith("Major", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Minor", StringComparison.OrdinalIgnoreCase))
        {
            if (text.Length <= 5)
                return string.Empty;

            return text[5..].TrimStart(' ', ':', '-').Trim();
        }

        return text;
    }

    private static string NormalizeToken(string? value)
    {
        var text = value ?? string.Empty;
        var chars = text.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static IReadOnlyList<FilterOption> BuildSpellColourOptions()
    {
        var options = Enum.GetValues<MagicColours>()
            .Select(c => new FilterOption(
                Token: NormalizeToken(c.ToString()),
                Label: EnumDisplayFormatter.Format(c)))
            .ToList();

        if (options.All(o => !string.Equals(o.Label, "Sorcorial", StringComparison.OrdinalIgnoreCase)))
            options.Add(new FilterOption(NormalizeToken("Sorcorial"), "Sorcorial"));

        return options;
    }

    private static IReadOnlyList<FilterOption> BuildAbilityTableOptions()
    {
        return Enum.GetValues<AbilityTables>()
            .OrderBy(table => (int)table)
            .Select(table =>
            {
                var tableValue = (int)table;
                var token = FormatAbilityTableToken(tableValue);
                return new FilterOption(token, token);
            })
            .ToList();
    }

    private static string FormatAbilityTableToken(int table)
        => table.ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyList<FilterOption> BuildMiracleSphereOptions()
    {
        return Enum.GetValues<SpiritualSpheres>()
            .Select(s => StripMajorMinorPrefix(EnumDisplayFormatter.Format(s)))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Where(label => !string.Equals(NormalizeToken(label), "universal", StringComparison.OrdinalIgnoreCase))
            .Select(label => new FilterOption(NormalizeToken(label), label))
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<FilterOption> BuildEvocationFieldOptions()
    {
        return Enum.GetValues<EvocationFields>()
            .Select(field => new FilterOption(
                Token: NormalizeToken(field.ToString()),
                Label: EnumDisplayFormatter.Format(field)))
            .ToList();
    }

    private static GlobalSearchResultVm CreateAbilityResult(EvolutionService.AbilityResult ability)
    {
        var title = (ability.Index ?? string.Empty).Trim();
        var avail = BuildAvailabilityDisplay(ability.Available);
        var cost = BuildAbilityCostDisplay(ability);
        var meta = avail.Length > 0
            ? $"Ability · Table {ability.Table} · Cost {cost} · {avail}"
            : $"Ability · Table {ability.Table} · Cost {cost}";

        if (ability.IsNonStandard)
            meta += " · Non-standard";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Ability,
            Name: title,
            GroupText: $"Table {ability.Table}",
            IconGlyph: "\uf013",
            MetaText: meta,
            DescriptionText: (ability.Description ?? string.Empty).Trim(),
            Ability: ability,
            Spell: null,
            Miracle: null,
            Evocation: null);
    }

    private static string BuildAbilityCostDisplay(EvolutionService.AbilityResult ability)
    {
        var cost = Math.Max(0, ability.Cost);
        if (!ability.CanBuyMultiple)
            return cost.ToString();

        if (ability.MaxAvailable is { } max && max > 0)
            return $"({cost}/{max})";

        return $"({cost}/∞)";
    }

    private static string BuildAvailabilityDisplay(string? rawAvailability)
    {
        var text = (rawAvailability ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return (doc.RootElement.GetString() ?? string.Empty).Trim();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var parts = doc.RootElement
                    .EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => (e.GetString() ?? string.Empty).Trim())
                    .Where(e => e.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (!property.Name.Equals("Display", StringComparison.OrdinalIgnoreCase)
                        && !property.Name.Equals("Label", StringComparison.OrdinalIgnoreCase)
                        && !property.Name.Equals("Value", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (property.Value.ValueKind == JsonValueKind.String)
                        return (property.Value.GetString() ?? string.Empty).Trim();

                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        var parts = property.Value
                            .EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => (e.GetString() ?? string.Empty).Trim())
                            .Where(e => e.Length > 0)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
                    }
                }
            }
        }
        catch
        {
            // non-JSON availability string
        }

        return text;
    }

    private static GlobalSearchResultVm CreateSpellResult(SpellService.SpellRaw spell)
    {
        var colour = (spell.colour ?? string.Empty).Trim();
        var meta = colour.Length > 0
            ? $"Spell · Lvl {Math.Max(0, spell.level)} · {colour}"
            : $"Spell · Lvl {Math.Max(0, spell.level)}";

        if (spell.nonStandard)
            meta += " · Non-standard";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Spell,
            Name: (spell.name ?? string.Empty).Trim(),
            GroupText: colour,
            IconGlyph: "\uf518",
            MetaText: meta,
            DescriptionText: (spell.description ?? string.Empty).Trim(),
            Ability: null,
            Spell: spell,
            Miracle: null,
            Evocation: null);
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

        if (miracle.nonStandard)
            meta += " · Non-standard";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Miracle,
            Name: (miracle.name ?? string.Empty).Trim(),
            GroupText: sphere,
            IconGlyph: "\uf005",
            MetaText: meta,
            DescriptionText: (miracle.description ?? string.Empty).Trim(),
            Ability: null,
            Spell: null,
            Miracle: miracle,
            Evocation: null);
    }

    private static GlobalSearchResultVm CreateEvocationResult(DruidEvocationService.EvocRaw evocation)
    {
        var fields = (evocation.fields ?? new List<string>())
            .Select(f => (f ?? string.Empty).Trim())
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var meta = fields.Count > 0
            ? $"Evocation · {Math.Max(0, evocation.power)} EP · {string.Join(", ", fields)}"
            : $"Evocation · {Math.Max(0, evocation.power)} EP";

        if (evocation.nonStandard)
            meta += " · Non-standard";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Evocation,
            Name: (evocation.name ?? string.Empty).Trim(),
            GroupText: string.Join(", ", fields),
            IconGlyph: "\uf06c",
            MetaText: meta,
            DescriptionText: (evocation.description ?? string.Empty).Trim(),
            Ability: null,
            Spell: null,
            Miracle: null,
            Evocation: evocation);
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

    private enum SearchSecondaryFilterMode
    {
        None,
        Ability,
        Spell,
        Miracle,
        Evocation
    }

    private readonly record struct FilterOption(string Token, string Label);
}

public enum GlobalSearchKind
{
    Ability,
    Spell,
    Miracle,
    Evocation
}

public enum GlobalSearchFilterTransition
{
    None,
    ToSecondary,
    ToPrimary
}

public sealed class GlobalSearchFilterChipVm : INotifyPropertyChanged
{
    private string _label;
    private bool _isSelected;
    private bool _isBack;

    public GlobalSearchFilterChipVm(string key, string label, bool isSelected, bool isBack)
    {
        Key = key ?? string.Empty;
        _label = label ?? string.Empty;
        _isSelected = isSelected;
        _isBack = isBack;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string Label
    {
        get => _label;
        private set
        {
            if (_label == value)
                return;

            _label = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        private set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public bool IsBack
    {
        get => _isBack;
        private set
        {
            if (_isBack == value)
                return;

            _isBack = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBack)));
        }
    }

    public void UpdateFrom(GlobalSearchFilterChipVm source)
    {
        if (source == null)
            return;

        Label = source.Label;
        IsSelected = source.IsSelected;
        IsBack = source.IsBack;
    }
}

public sealed record GlobalSearchResultVm(
    GlobalSearchKind Kind,
    string Name,
    string GroupText,
    string IconGlyph,
    string MetaText,
    string DescriptionText,
    EvolutionService.AbilityResult? Ability,
    SpellService.SpellRaw? Spell,
    MiracleService.MiracRaw? Miracle,
    DruidEvocationService.EvocRaw? Evocation)
{
    public string DetailKey { get; init; } = string.Empty;

    public int? AbilityTable { get; init; }

    public bool CanOpenDetails
        => Ability != null
           || Spell != null
           || Miracle != null
           || Evocation != null
           || Kind is GlobalSearchKind.Ability
               or GlobalSearchKind.Spell
               or GlobalSearchKind.Miracle
               or GlobalSearchKind.Evocation;

    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);

    public string IconBackgroundColor => Kind switch
    {
        GlobalSearchKind.Ability => "#E0F2FE",
        GlobalSearchKind.Spell => "#DBEAFE",
        GlobalSearchKind.Miracle => "#FEF3C7",
        GlobalSearchKind.Evocation => "#DCFCE7",
        _ => "#E5E7EB"
    };

    public string IconBorderColor => Kind switch
    {
        GlobalSearchKind.Ability => "#7DD3FC",
        GlobalSearchKind.Spell => "#93C5FD",
        GlobalSearchKind.Miracle => "#FCD34D",
        GlobalSearchKind.Evocation => "#86EFAC",
        _ => "#D1D5DB"
    };

    public string IconColor => Kind switch
    {
        GlobalSearchKind.Ability => "#0C4A6E",
        GlobalSearchKind.Spell => "#1E3A8A",
        GlobalSearchKind.Miracle => "#92400E",
        GlobalSearchKind.Evocation => "#166534",
        _ => "#374151"
    };

}
