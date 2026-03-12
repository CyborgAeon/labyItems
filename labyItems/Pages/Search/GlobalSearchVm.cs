using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using labyItems.Controls;
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

    private readonly List<GlobalSearchResultVm> _allResults = new();
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

            ApplyFilters();
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
            ApplyFilters();
            return;
        }

        IsLoading = true;
        try
        {
            var evolutionAbilities = await SafeLoadAsync(EvolutionService.GetAllAbilitiesAsync);
            var spells = await SafeLoadAsync(async () =>
            {
                var list = await SpellService.GetAllAsync();
                return (IReadOnlyList<SpellService.SpellRaw>)list;
            });
            var miracles = await SafeLoadAsync(MiracleService.GetAllAsync);
            var evocations = await SafeLoadEvocationsAsync();

            BuildUnifiedResults(evolutionAbilities, spells, miracles, evocations);
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
        IReadOnlyList<SpellService.SpellRaw> spells,
        IReadOnlyList<MiracleService.MiracRaw> miracles,
        IReadOnlyList<DruidEvocationService.EvocRaw> evocations)
    {
        _allResults.Clear();

        var abilityMap = new Dictionary<string, GlobalSearchResultVm>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in evolutionAbilities)
        {
            var key = BuildAbilityKey(ability.Index, ability.Table);
            if (abilityMap.ContainsKey(key))
                continue;

            abilityMap[key] = CreateAbilityResult(ability);
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
        var selectedKind = ResolveFilterKind(_selectedPrimaryFilter);

        IEnumerable<GlobalSearchResultVm> results = _allResults;

        if (selectedKind.HasValue)
            results = results.Where(r => r.Kind == selectedKind.Value);

        if (selectedKind == GlobalSearchKind.Spell && _secondaryFilterMode == SearchSecondaryFilterMode.Spell)
            results = results.Where(r => r.Spell != null && PassesSpellSubFilters(r.Spell));

        if (selectedKind == GlobalSearchKind.Ability && _secondaryFilterMode == SearchSecondaryFilterMode.Ability)
            results = results.Where(r => r.Ability != null && PassesAbilitySubFilters(r.Ability));

        if (selectedKind == GlobalSearchKind.Miracle && _secondaryFilterMode == SearchSecondaryFilterMode.Miracle)
            results = results.Where(r => r.Miracle != null && PassesMiracleSubFilters(r.Miracle));

        if (selectedKind == GlobalSearchKind.Evocation && _secondaryFilterMode == SearchSecondaryFilterMode.Evocation)
            results = results.Where(r => r.Evocation != null && PassesEvocationSubFilters(r.Evocation));

        List<GlobalSearchResultVm> ordered;
        if (normalized.Length > 0)
        {
            ordered = results
                .Where(r => MatchesQuery(r, normalized))
                .Select(r => new
                {
                    Result = r,
                    NameRank = ComputeFieldMatchRank(r.Name, normalized),
                    GroupRank = ComputeFieldMatchRank(r.GroupText, normalized),
                    DescriptionRank = ComputeFieldMatchRank(r.DescriptionText, normalized)
                })
                .OrderBy(x => x.NameRank.MatchType)
                .ThenBy(x => x.NameRank.Position)
                .ThenBy(x => x.NameRank.LengthDelta)
                .ThenBy(x => x.GroupRank.MatchType)
                .ThenBy(x => x.GroupRank.Position)
                .ThenBy(x => x.GroupRank.LengthDelta)
                .ThenBy(x => x.DescriptionRank.MatchType)
                .ThenBy(x => x.DescriptionRank.Position)
                .ThenBy(x => x.DescriptionRank.LengthDelta)
                .ThenBy(x => x.Result.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Result.Kind)
                .Take(MaxVisibleResults)
                .Select(x => x.Result)
                .ToList();
        }
        else
        {
            ordered = results
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Kind)
                .Take(MaxVisibleResults)
                .ToList();
        }

        FilteredResults = ordered;
    }

    private bool PassesSpellSubFilters(SpellService.SpellRaw spell)
    {
        if (!PassesTierFilter(_selectedSpellTierTokens, spell.isAdvanced ?? false))
            return false;

        if (_selectedSpellColourTokens.Count == 0)
            return true;

        return _selectedSpellColourTokens.Any(token => SpellMatchesColourToken(spell, token));
    }

    private bool PassesAbilitySubFilters(EvolutionService.AbilityResult ability)
    {
        if (_selectedAbilityTableTokens.Count == 0)
            return true;

        var abilityTableToken = FormatAbilityTableToken(ability.Table);
        return _selectedAbilityTableTokens.Contains(abilityTableToken);
    }

    private bool PassesMiracleSubFilters(MiracleService.MiracRaw miracle)
    {
        if (!PassesTierFilter(_selectedMiracleTierTokens, miracle.isAdvanced))
            return false;

        if (_selectedMiracleSphereTokens.Count == 0)
            return true;

        var sphereToken = NormalizeMiracleSphereToken(miracle.sphere);
        if (sphereToken == "universal")
            return true;

        return _selectedMiracleSphereTokens.Contains(sphereToken);
    }

    private bool PassesEvocationSubFilters(DruidEvocationService.EvocRaw evocation)
    {
        if (!PassesTierFilter(_selectedEvocationTierTokens, evocation.isAdvanced))
            return false;

        if (_selectedEvocationFieldTokens.Count == 0)
            return true;

        var fieldTokens = ExtractEvocationFieldTokens(evocation.fields);
        return fieldTokens.Overlaps(_selectedEvocationFieldTokens);
    }

    private static bool PassesTierFilter(HashSet<string> selectedTierTokens, bool isAdvanced)
    {
        if (selectedTierTokens.Count != 1)
            return true;

        if (selectedTierTokens.Contains(NormalizeToken(TierAdvanced)))
            return isAdvanced;

        if (selectedTierTokens.Contains(NormalizeToken(TierHandbook)))
            return !isAdvanced;

        return true;
    }

    private static bool SpellMatchesColourToken(SpellService.SpellRaw spell, string selectedToken)
    {
        var rawColour = (spell.colour ?? string.Empty).Trim();
        if (rawColour.Length == 0)
            return false;

        var normalizedRaw = NormalizeToken(rawColour);
        if (selectedToken == NormalizeToken("Sorcorial"))
            return normalizedRaw.Contains("sorc", StringComparison.OrdinalIgnoreCase);

        var split = rawColour
            .Split([',', '/', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken)
            .Where(t => t.Length > 0)
            .ToList();

        if (split.Any(t => string.Equals(t, selectedToken, StringComparison.OrdinalIgnoreCase)))
            return true;

        return normalizedRaw.Contains(selectedToken, StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> ExtractEvocationFieldTokens(IEnumerable<string>? rawFields)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasAll = false;

        foreach (var raw in rawFields ?? Array.Empty<string>())
        {
            var token = CanonicalizeEvocationFieldToken(NormalizeToken(raw));
            if (token.Length == 0)
                continue;

            if (token == "all")
            {
                hasAll = true;
                continue;
            }

            if (EvocationFieldTokens.Contains(token))
                tokens.Add(token);
        }

        if (hasAll)
        {
            foreach (var token in EvocationFieldTokens)
                tokens.Add(token);
        }

        return tokens;
    }

    private static string CanonicalizeEvocationFieldToken(string token)
    {
        if (EvocationFieldAliases.TryGetValue(token, out var canonical))
            return canonical;

        return token;
    }

    private static bool MatchesQuery(GlobalSearchResultVm result, string query)
    {
        return ContainsQuery(result.Name, query)
            || ContainsQuery(result.GroupText, query)
            || ContainsQuery(result.DescriptionText, query);
    }

    private static bool ContainsQuery(string? value, string query)
    {
        if (query.Length == 0)
            return true;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static (int MatchType, int Position, int LengthDelta) ComputeFieldMatchRank(string? value, string query)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (4, int.MaxValue, int.MaxValue);

        var text = value.ToLowerInvariant();
        if (text.Equals(query, StringComparison.Ordinal))
            return (0, 0, 0);

        if (text.StartsWith(query, StringComparison.Ordinal))
            return (1, 0, text.Length - query.Length);

        var wordStartIndex = FindWordStartIndex(text, query);
        if (wordStartIndex >= 0)
            return (2, wordStartIndex, text.Length - query.Length);

        var containsIndex = text.IndexOf(query, StringComparison.Ordinal);
        if (containsIndex >= 0)
            return (3, containsIndex, text.Length - query.Length);

        return (4, int.MaxValue, int.MaxValue);
    }

    private static int FindWordStartIndex(string text, string query)
    {
        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(query, start, StringComparison.Ordinal);
            if (index < 0)
                return -1;

            if (index == 0 || !char.IsLetterOrDigit(text[index - 1]))
                return index;

            start = index + 1;
        }

        return -1;
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

    private static async Task<IReadOnlyList<DruidEvocationService.EvocRaw>> SafeLoadEvocationsAsync()
    {
        return await EvocationCatalogService.GetAllAsync();
    }

    private static string BuildAbilityKey(string? name, int table)
        => $"{(name ?? string.Empty).Trim().ToLowerInvariant()}|{table}";

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
    public bool CanOpenDetails => Ability != null || Spell != null || Miracle != null || Evocation != null;

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
