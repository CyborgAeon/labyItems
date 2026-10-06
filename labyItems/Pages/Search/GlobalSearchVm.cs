using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Infrastructure;
using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Pages.Search;

public sealed class GlobalSearchVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private const int MaxVisibleResults = 300;
    private const string TierHandbook = "Handbook";
    private const string TierAdvanced = "Advanced";
    private const string KindKeyPrefix = "kind:";

    private static readonly IReadOnlyList<(GlobalSearchKind Kind, string Label)> KindOrder =
    [
        (GlobalSearchKind.Spell, "Spells"),
        (GlobalSearchKind.Miracle, "Miracles"),
        (GlobalSearchKind.Evocation, "Evocations"),
        (GlobalSearchKind.Neuronic, "Neuronics"),
        (GlobalSearchKind.Ability, "Abilities")
    ];

    private static readonly IReadOnlyList<FilterOption> SpellColourOptions = BuildSpellColourOptions();
    private static readonly IReadOnlyList<FilterOption> AbilityTableOptions = BuildAbilityTableOptions();
    private static readonly IReadOnlyList<FilterOption> MiracleSphereOptions = BuildMiracleSphereOptions();
    private static readonly IReadOnlyList<FilterOption> EvocationFieldOptions = BuildEvocationFieldOptions();
    private static readonly IReadOnlyList<FilterOption> NeuroTypeOptions = BuildNeuroTypeOptions();
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

    private FilterSnapshot _applied = FilterSnapshot.Empty();
    private FilterSnapshot _pending = FilterSnapshot.Empty();

    private bool _isLoaded;

    public ObservableCollection<GlobalSearchFilterChipVm> KindChips { get; } = new();
    public ObservableCollection<GlobalSearchFilterSectionVm> FilterSections { get; } = new();

    public ICommand OpenFiltersCommand { get; }
    public ICommand CancelFiltersCommand { get; }
    public ICommand ApplyFiltersCommand { get; }
    public ICommand ToggleFilterChipCommand { get; }

    private bool _isFilterModalOpen;
    public bool IsFilterModalOpen
    {
        get => _isFilterModalOpen;
        private set => Set(ref _isFilterModalOpen, value);
    }

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
                return "Type to search spells, miracles, evocations, neuronics, and abilities.";

            return "No matches found.";
        }
    }

    public GlobalSearchVm(GlobalSearchPickerOptions? picker = null)
    {
        _picker = picker;
        if (picker != null && !picker.AllowAllKinds)
        {
            _applied.Kinds.Add(picker.Kind);
            _pending.Kinds.Add(picker.Kind);
        }
        else if (picker?.AllowAllKinds == true)
        {
            foreach (var (kind, _) in KindOrder)
            {
                _applied.Kinds.Add(kind);
                _pending.Kinds.Add(kind);
            }
        }

        _searchDebounce = new DebouncedAsyncAction(300, ApplyFiltersAsync);
        OpenFiltersCommand = new Command(OpenFilters);
        CancelFiltersCommand = new Command(CancelFilters);
        ApplyFiltersCommand = new Command(ApplyPendingFilters);
        ToggleFilterChipCommand = new Command<GlobalSearchFilterChipVm>(ToggleFilterChip);
        ToggleResultSelectionCommand = new Command<GlobalSearchResultVm>(ToggleResultSelection);
    }

    private readonly GlobalSearchPickerOptions? _picker;
    private readonly Dictionary<string, GlobalSearchResultVm> _selectedResults = new(StringComparer.OrdinalIgnoreCase);

    public ICommand ToggleResultSelectionCommand { get; }
    public bool IsPickerMode => _picker != null;
    public bool ShowKindChips => _picker == null || _picker.AllowAllKinds;
    public string PickerTitle => _picker?.Title ?? string.Empty;
    public string PickerConfirmText => _picker?.ConfirmText ?? "Save";

    public string SearchPlaceholder => _picker == null
        ? "Search abilities, spells, miracles, evocations, neuronics..."
        : "Search...";

    private static string SelectionKey(GlobalSearchResultVm result)
        => $"{result.Kind}|{(string.IsNullOrWhiteSpace(result.DetailKey) ? result.Name : result.DetailKey)}";

    private void ToggleResultSelection(GlobalSearchResultVm? result)
    {
        if (result == null)
            return;

        var key = SelectionKey(result);
        if (_selectedResults.Remove(key))
        {
            result.IsSelected = false;
        }
        else
        {
            if (_picker?.SingleSelection == true)
            {
                foreach (var selected in _selectedResults.Values)
                    selected.IsSelected = false;
                _selectedResults.Clear();
            }

            result.IsSelected = true;
            _selectedResults[key] = result;
        }
    }

    public IReadOnlyList<GlobalSearchResultVm> GetPickedResults() => _selectedResults.Values.ToList();

    private void OpenFilters()
    {
        _pending = _applied.Clone();
        RebuildModal();
        IsFilterModalOpen = true;
    }

    private void CancelFilters() => IsFilterModalOpen = false;

    private void ApplyPendingFilters()
    {
        _applied = _pending.Clone();
        IsFilterModalOpen = false;
        ApplyFilters();
    }

    private void ToggleFilterChip(GlobalSearchFilterChipVm? chip)
    {
        if (chip == null || !chip.IsEnabled)
            return;

        if (chip.Key.StartsWith(KindKeyPrefix, StringComparison.Ordinal))
        {
            if (_picker != null || !Enum.TryParse<GlobalSearchKind>(chip.Key[KindKeyPrefix.Length..], out var kind))
                return;

            if (!_pending.Kinds.Add(kind))
                _pending.Kinds.Remove(kind);
        }
        else
        {
            var separator = chip.Key.IndexOf(':');
            if (separator <= 0)
                return;

            var set = _pending.GetTokens(chip.Key[..(separator + 1)]);
            var token = chip.Key[(separator + 1)..];
            if (set == null || token.Length == 0)
                return;

            if (!set.Add(token))
                set.Remove(token);
        }

        RebuildModal();
    }

    private void RebuildModal()
    {
        var kindChips = KindOrder
            .Select(k => new GlobalSearchFilterChipVm($"{KindKeyPrefix}{k.Kind}", k.Label, _pending.Kinds.Contains(k.Kind), false))
            .ToList();
        PatchCollection(KindChips, kindChips);

        var sections = new List<GlobalSearchFilterSectionVm>();
        foreach (var (kind, label) in KindOrder)
        {
            if (!_pending.Kinds.Contains(kind))
                continue;

            var chips = BuildSectionChips(kind);
            if (chips.Count == 0)
                continue;

            var existing = FilterSections.FirstOrDefault(s => s.Kind == kind);
            var section = existing ?? new GlobalSearchFilterSectionVm(kind, label);
            PatchCollection(section.Chips, chips);
            sections.Add(section);
        }

        if (sections.Count == FilterSections.Count && sections.Zip(FilterSections).All(p => ReferenceEquals(p.First, p.Second)))
            return;

        FilterSections.Clear();
        foreach (var section in sections)
            FilterSections.Add(section);
    }

    private static void PatchCollection(ObservableCollection<GlobalSearchFilterChipVm> target, List<GlobalSearchFilterChipVm> desired)
    {
        if (target.Count == desired.Count
            && target.Zip(desired).All(p => string.Equals(p.First.Key, p.Second.Key, StringComparison.Ordinal)))
        {
            for (var i = 0; i < desired.Count; i++)
                target[i].UpdateFrom(desired[i]);
            return;
        }

        target.Clear();
        foreach (var chip in desired)
            target.Add(chip);
    }

    private List<GlobalSearchFilterChipVm> BuildSectionChips(GlobalSearchKind kind)
    {
        var chips = new List<GlobalSearchFilterChipVm>();
        switch (kind)
        {
            case GlobalSearchKind.Spell:
                AddTierChips(chips, "spell-tier:", _pending.SpellTiers);
                AddOptionChips(chips, "spell-colour:", SpellColourOptions, _pending.SpellColours);
                break;
            case GlobalSearchKind.Ability:
                AddOptionChips(chips, "ability-table:", AbilityTableOptions, _pending.AbilityTables);
                break;
            case GlobalSearchKind.Miracle:
                AddTierChips(chips, "miracle-tier:", _pending.MiracleTiers);
                AddOptionChips(chips, "miracle-sphere:", MiracleSphereOptions, _pending.MiracleSpheres);
                break;
            case GlobalSearchKind.Evocation:
                AddTierChips(chips, "evocation-tier:", _pending.EvocationTiers);
                AddOptionChips(chips, "evocation-field:", EvocationFieldOptions, _pending.EvocationFields);
                break;
            case GlobalSearchKind.Neuronic:
                AddOptionChips(chips, "neuro-type:", NeuroTypeOptions, _pending.NeuroTypes);
                break;
        }

        if (_picker?.IsFilterAvailable is { } isAvailable)
        {
            foreach (var chip in chips)
                chip.IsEnabled = isAvailable(chip.Key);
        }

        return chips;
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

        var snapshot = _applied.Clone();
        var allFilters = snapshot.ToFilterSet();

        try
        {
            IReadOnlyList<OptimizedSearchService.SearchResultDto> dtos;
            if (snapshot.Kinds.Count == 0)
            {
                var all = await _optimizedSearchService.SearchAllAsync(
                    _searchText,
                    filterKind: null,
                    pageNumber: 1,
                    pageSize: MaxVisibleResults,
                    cancellationToken: cancellationToken);
                dtos = all.Results;
            }
            else
            {
                // Whitelist: each selected kind is searched with its own sub-filters, then merged.
                var merged = new List<OptimizedSearchService.SearchResultDto>();
                // Picker scope is applied after the query, so fetch the full set first.
                var fetchSize = _picker?.AllowedNames != null ? 5000 : MaxVisibleResults;
                foreach (var kind in snapshot.Kinds)
                {
                    var part = await _optimizedSearchService.SearchByKindAsync(
                        kind,
                        _searchText,
                        allFilters,
                        pageNumber: 1,
                        pageSize: fetchSize,
                        cancellationToken: cancellationToken);
                    merged.AddRange(part.Results);
                }

                if (_picker?.AllowedNames is { } allowed)
                    merged = merged.Where(r => allowed.Contains(r.Name)).ToList();

                dtos = merged
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.Kind)
                    .Take(MaxVisibleResults)
                    .ToList();
            }

            var converted = await Task.Run(
                () => ConvertOptimizedResults(dtos),
                cancellationToken);

            if (_picker?.AllowAllKinds == true)
            {
                var query = (_searchText ?? string.Empty).Trim();
                var guildNames = await GuildsService.GetGuildNamesAsync();
                converted.AddRange(guildNames
                    .Where(name => query.Length == 0 || name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Select(name => new GlobalSearchResultVm(
                        GlobalSearchKind.Guild, name, "Guild", "\uf0c0", "Guild", string.Empty,
                        null, null, null, null, null)));
                converted = converted
                    .OrderBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(MaxVisibleResults)
                    .ToList();
            }

            if (_picker?.InitialSelectedNames is { Count: > 0 } initial)
            {
                foreach (var result in converted.Where(result => initial.Contains(result.Name)))
                {
                    result.IsSelected = true;
                    _selectedResults.TryAdd(SelectionKey(result), result);
                }
            }
            
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
                GlobalSearchKind.Neuronic => CreateNeuronicResultFromDto(result),
                _ => null
            };

            if (vm != null)
            {
                if (_selectedResults.TryGetValue(SelectionKey(vm), out var selected))
                {
                    vm = selected;
                }

                converted.Add(vm);
            }
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
            Evocation: null,
            Neuronic: null)
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
            Evocation: null,
            Neuronic: null)
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
            Evocation: null,
            Neuronic: null)
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
            Evocation: null,
            Neuronic: null)
        {
            DetailKey = string.IsNullOrWhiteSpace(dto.LookupKey) ? dto.Name : dto.LookupKey
        };
    }

    private GlobalSearchResultVm CreateNeuronicResultFromDto(OptimizedSearchService.SearchResultDto dto)
    {
        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Neuronic,
            Name: dto.Name,
            GroupText: dto.ExtraInfo ?? string.Empty,
            IconGlyph: "\uf5dc",
            MetaText: $"Neuronic - {dto.ExtraInfo}",
            DescriptionText: dto.Description,
            Ability: null,
            Spell: null,
            Miracle: null,
            Evocation: null,
            Neuronic: null)
        {
            DetailKey = string.IsNullOrWhiteSpace(dto.LookupKey) ? dto.Name : dto.LookupKey
        };
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

    private static IReadOnlyList<FilterOption> BuildNeuroTypeOptions()
    {
        return new[]
        {
            new FilterOption(NormalizeToken(NeuroOptionType.Active.ToString()), "Active"),
            new FilterOption(NormalizeToken(NeuroOptionType.Passive.ToString()), "Passive")
        };
    }

    private static GlobalSearchResultVm CreateAbilityResult(EvolutionService.AbilityResult ability)
    {
        var title = (string.IsNullOrWhiteSpace(ability.DisplayName)
            ? ability.Index
            : ability.DisplayName).Trim();
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
            Evocation: null,
            Neuronic: null);
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
            Evocation: null,
            Neuronic: null);
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
            Evocation: null,
            Neuronic: null);
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
            Evocation: evocation,
            Neuronic: null);
    }

    private static GlobalSearchResultVm CreateNeuronicResult(NeuronicService.NeuronicRaw neuronic)
    {
        var type = NeuronicService.FormatType(neuronic.Type);
        var meta = $"Neuronic - {Math.Max(0, neuronic.power)} TBLP";
        if (!type.Equals("None", StringComparison.OrdinalIgnoreCase))
            meta += $" - {type}";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Neuronic,
            Name: (neuronic.name ?? string.Empty).Trim(),
            GroupText: type,
            IconGlyph: "\uf5dc",
            MetaText: meta,
            DescriptionText: (neuronic.description ?? string.Empty).Trim(),
            Ability: null,
            Spell: null,
            Miracle: null,
            Evocation: null,
            Neuronic: neuronic);
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

    private sealed class FilterSnapshot
    {
        public HashSet<GlobalSearchKind> Kinds { get; init; } = new();
        public HashSet<string> SpellColours { get; init; } = NewSet();
        public HashSet<string> SpellTiers { get; init; } = NewSet();
        public HashSet<string> AbilityTables { get; init; } = NewSet();
        public HashSet<string> MiracleSpheres { get; init; } = NewSet();
        public HashSet<string> MiracleTiers { get; init; } = NewSet();
        public HashSet<string> EvocationFields { get; init; } = NewSet();
        public HashSet<string> EvocationTiers { get; init; } = NewSet();
        public HashSet<string> NeuroTypes { get; init; } = NewSet();

        public static FilterSnapshot Empty() => new();

        private static HashSet<string> NewSet(IEnumerable<string>? source = null)
            => new(source ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        public FilterSnapshot Clone() => new()
        {
            Kinds = new HashSet<GlobalSearchKind>(Kinds),
            SpellColours = NewSet(SpellColours),
            SpellTiers = NewSet(SpellTiers),
            AbilityTables = NewSet(AbilityTables),
            MiracleSpheres = NewSet(MiracleSpheres),
            MiracleTiers = NewSet(MiracleTiers),
            EvocationFields = NewSet(EvocationFields),
            EvocationTiers = NewSet(EvocationTiers),
            NeuroTypes = NewSet(NeuroTypes)
        };

        public HashSet<string>? GetTokens(string prefix) => prefix switch
        {
            "spell-colour:" => SpellColours,
            "spell-tier:" => SpellTiers,
            "ability-table:" => AbilityTables,
            "miracle-sphere:" => MiracleSpheres,
            "miracle-tier:" => MiracleTiers,
            "evocation-field:" => EvocationFields,
            "evocation-tier:" => EvocationTiers,
            "neuro-type:" => NeuroTypes,
            _ => null
        };

        public HashSet<string> ToFilterSet()
        {
            var filters = NewSet();
            filters.UnionWith(SpellColours.Select(t => $"spell-colour:{t}"));
            filters.UnionWith(SpellTiers.Select(t => $"spell-tier:{t}"));
            filters.UnionWith(AbilityTables.Select(t => $"ability-table:{t}"));
            filters.UnionWith(MiracleSpheres.Select(t => $"miracle-sphere:{t}"));
            filters.UnionWith(MiracleTiers.Select(t => $"miracle-tier:{t}"));
            filters.UnionWith(EvocationFields.Select(t => $"evocation-field:{t}"));
            filters.UnionWith(EvocationTiers.Select(t => $"evocation-tier:{t}"));
            filters.UnionWith(NeuroTypes.Select(t => $"neuro-type:{t}"));
            return filters;
        }
    }

    private readonly record struct FilterOption(string Token, string Label);
}

public enum GlobalSearchKind
{
    Ability,
    Spell,
    Miracle,
    Evocation,
    Neuronic,
    Guild
}

public sealed class GlobalSearchFilterSectionVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public GlobalSearchFilterSectionVm(GlobalSearchKind kind, string title)
    {
        Kind = kind;
        Title = title;
        ToggleExpandedCommand = new Command(() => IsExpanded = !IsExpanded);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GlobalSearchKind Kind { get; }
    public string Title { get; }
    public ObservableCollection<GlobalSearchFilterChipVm> Chips { get; } = new();
    public ICommand ToggleExpandedCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        private set
        {
            if (_isExpanded == value)
                return;

            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }
}

public sealed class GlobalSearchFilterChipVm : INotifyPropertyChanged
{
    private string _label;
    private bool _isSelected;
    private bool _isBack;
    private bool _isEnabled = true;

    public GlobalSearchFilterChipVm(string key, string label, bool isSelected, bool isBack)
    {
        Key = key ?? string.Empty;
        _label = label ?? string.Empty;
        _isSelected = isSelected;
        _isBack = isBack;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    // False when the filter can't match anything in the current search scope.
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChipOpacity)));
        }
    }

    public double ChipOpacity => _isEnabled ? 1d : 0.35d;

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
        IsEnabled = source.IsEnabled;
    }
}

// Restricts the search page to one kind and turns it into a multi-select picker.
public sealed class GlobalSearchPickerOptions
{
    public required GlobalSearchKind Kind { get; init; }
    public required string Title { get; init; }
    public IReadOnlySet<string>? AllowedNames { get; init; }
    public Func<string, bool>? IsFilterAvailable { get; init; }
    public string ConfirmText { get; init; } = "Save";
    public bool SingleSelection { get; init; }
    public bool AllowAllKinds { get; init; }
    public IReadOnlySet<string>? InitialSelectedNames { get; init; }
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
    DruidEvocationService.EvocRaw? Evocation,
    NeuronicService.NeuronicRaw? Neuronic) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionGlyph)));
        }
    }

    public string SelectionGlyph => IsSelected ? "\uf14a" : "\uf0c8";

    // Reference equality: selection state is mutable.
    public bool Equals(GlobalSearchResultVm? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    public string DetailKey { get; init; } = string.Empty;

    public int? AbilityTable { get; init; }

    public bool CanOpenDetails
        => Ability != null
           || Spell != null
           || Miracle != null
           || Evocation != null
           || Neuronic != null
           || Kind is GlobalSearchKind.Ability
               or GlobalSearchKind.Spell
               or GlobalSearchKind.Miracle
               or GlobalSearchKind.Evocation
               or GlobalSearchKind.Neuronic;

    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);

    public string IconBackgroundColor => Kind switch
    {
        GlobalSearchKind.Ability => "#E0F2FE",
        GlobalSearchKind.Spell => "#DBEAFE",
        GlobalSearchKind.Miracle => "#FEF3C7",
        GlobalSearchKind.Evocation => "#DCFCE7",
        GlobalSearchKind.Neuronic => "#E0E7FF",
        GlobalSearchKind.Guild => "#F3E8FF",
        _ => "#E5E7EB"
    };

    public string IconBorderColor => Kind switch
    {
        GlobalSearchKind.Ability => "#7DD3FC",
        GlobalSearchKind.Spell => "#93C5FD",
        GlobalSearchKind.Miracle => "#FCD34D",
        GlobalSearchKind.Evocation => "#86EFAC",
        GlobalSearchKind.Neuronic => "#A5B4FC",
        GlobalSearchKind.Guild => "#D8B4FE",
        _ => "#D1D5DB"
    };

    public string IconColor => Kind switch
    {
        GlobalSearchKind.Ability => "#0C4A6E",
        GlobalSearchKind.Spell => "#1E3A8A",
        GlobalSearchKind.Miracle => "#92400E",
        GlobalSearchKind.Evocation => "#166534",
        GlobalSearchKind.Neuronic => "#3730A3",
        GlobalSearchKind.Guild => "#581C87",
        _ => "#374151"
    };

}
