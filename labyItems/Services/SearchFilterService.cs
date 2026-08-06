using System.Globalization;
using labyItems.Models.Enums;
using labyItems.Pages.Search;

namespace labyItems.Services;

public sealed class SearchFilterService
{
    private const int MaxVisibleResults = 300;

    public async Task<IReadOnlyList<GlobalSearchResultVm>> FilterAndSortAsync(
        IReadOnlyList<GlobalSearchResultVm> allResults,
        string searchText,
        GlobalSearchKind? selectedKind,
        IReadOnlySet<string> selectedFilters,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(
            () => PerformFiltersAndSort(allResults, searchText, selectedKind, selectedFilters),
            cancellationToken);
    }

    private IReadOnlyList<GlobalSearchResultVm> PerformFiltersAndSort(
        IReadOnlyList<GlobalSearchResultVm> allResults,
        string searchText,
        GlobalSearchKind? selectedKind,
        IReadOnlySet<string> selectedFilters)
    {
        var query = (searchText ?? string.Empty).Trim();
        var normalized = NormalizeForSearch(query);

        IEnumerable<GlobalSearchResultVm> results = allResults;

        if (selectedKind.HasValue)
            results = results.Where(r => r.Kind == selectedKind.Value);

        // Apply secondary filters based on kind
        results = ApplySecondaryFilters(results, selectedKind, selectedFilters);

        if (!string.IsNullOrEmpty(normalized))
        {
            results = results
                .Select(r => new
                {
                    Result = r,
                    NameRank = ComputeFieldMatchRank(r.Name, normalized),
                    GroupRank = ComputeFieldMatchRank(r.GroupText, normalized)
                })
                .Where(x => x.NameRank.MatchType != MatchType.None
                            || x.GroupRank.MatchType != MatchType.None)
                .OrderBy(x => x.NameRank.MatchType)
                .ThenBy(x => x.NameRank.Position)
                .ThenBy(x => x.NameRank.LengthDelta)
                .ThenBy(x => x.GroupRank.MatchType)
                .ThenBy(x => x.GroupRank.Position)
                .ThenBy(x => x.GroupRank.LengthDelta)
                .ThenBy(x => x.Result.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Result.Kind)
                .Take(MaxVisibleResults)
                .Select(x => x.Result);
        }
        else
        {
            results = results
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Kind)
                .Take(MaxVisibleResults);
        }

        return results.ToList();
    }

    private IEnumerable<GlobalSearchResultVm> ApplySecondaryFilters(
        IEnumerable<GlobalSearchResultVm> results,
        GlobalSearchKind? selectedKind,
        IReadOnlySet<string> selectedFilters)
    {
        if (selectedKind == GlobalSearchKind.Spell)
            return results.Where(r => r.Spell == null || PassesSpellSubFilters(r.Spell, selectedFilters));

        if (selectedKind == GlobalSearchKind.Ability)
            return results.Where(r => r.Ability == null || PassesAbilitySubFilters(r.Ability, selectedFilters));

        if (selectedKind == GlobalSearchKind.Miracle)
            return results.Where(r => r.Miracle == null || PassesMiracleSubFilters(r.Miracle, selectedFilters));

        if (selectedKind == GlobalSearchKind.Evocation)
            return results.Where(r => r.Evocation == null || PassesEvocationSubFilters(r.Evocation, selectedFilters));

        if (selectedKind == GlobalSearchKind.Neuronic)
            return results.Where(r => r.Neuronic == null || PassesNeuronicSubFilters(r.Neuronic, selectedFilters));

        return results;
    }

    private bool PassesSpellSubFilters(SpellService.SpellRaw spell, IReadOnlySet<string> selectedFilters)
    {
        var tierFilters = ExtractFiltersForPrefix(selectedFilters, "spell-tier:");
        if (tierFilters.Count > 0 && !PassesTierFilter(tierFilters, spell.isAdvanced ?? false))
            return false;

        var colourFilters = ExtractFiltersForPrefix(selectedFilters, "spell-colour:");
        if (colourFilters.Count == 0)
            return true;

        return colourFilters.Any(token => SpellMatchesColourToken(spell, token));
    }

    private bool PassesAbilitySubFilters(EvolutionService.AbilityResult ability, IReadOnlySet<string> selectedFilters)
    {
        var tableFilters = ExtractFiltersForPrefix(selectedFilters, "ability-table:");
        if (tableFilters.Count == 0)
            return true;

        var abilityTableToken = ability.Table.ToString(CultureInfo.InvariantCulture);
        return tableFilters.Contains(abilityTableToken);
    }

    private bool PassesMiracleSubFilters(MiracleService.MiracRaw miracle, IReadOnlySet<string> selectedFilters)
    {
        var tierFilters = ExtractFiltersForPrefix(selectedFilters, "miracle-tier:");
        if (tierFilters.Count > 0 && !PassesTierFilter(tierFilters, miracle.isAdvanced))
            return false;

        var sphereFilters = ExtractFiltersForPrefix(selectedFilters, "miracle-sphere:");
        if (sphereFilters.Count == 0)
            return true;

        var sphereToken = NormalizeMiracleSphereToken(miracle.sphere);
        if (sphereToken == "universal")
            return true;

        return sphereFilters.Contains(sphereToken);
    }

    private bool PassesEvocationSubFilters(DruidEvocationService.EvocRaw evocation, IReadOnlySet<string> selectedFilters)
    {
        var tierFilters = ExtractFiltersForPrefix(selectedFilters, "evocation-tier:");
        if (tierFilters.Count > 0 && !PassesTierFilter(tierFilters, evocation.isAdvanced))
            return false;

        var fieldFilters = ExtractFiltersForPrefix(selectedFilters, "evocation-field:");
        if (fieldFilters.Count == 0)
            return true;

        var fieldTokens = ExtractEvocationFieldTokens(evocation.fields);
        return fieldTokens.Overlaps(fieldFilters);
    }

    private bool PassesNeuronicSubFilters(NeuronicService.NeuronicRaw neuronic, IReadOnlySet<string> selectedFilters)
    {
        var typeFilters = ExtractFiltersForPrefix(selectedFilters, "neuro-type:");
        if (typeFilters.Count == 0)
            return true;

        var typeToken = NormalizeForSearch(NeuronicService.FormatType(neuronic.Type));
        return typeFilters.Contains(typeToken);
    }

    private static HashSet<string> ExtractFiltersForPrefix(IReadOnlySet<string> selectedFilters, string prefix)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in selectedFilters)
        {
            if (filter.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                result.Add(filter[prefix.Length..]);
        }
        return result;
    }

    private static bool PassesTierFilter(HashSet<string> selectedTierTokens, bool isAdvanced)
    {
        if (selectedTierTokens.Count != 1)
            return true;

        var token = selectedTierTokens.FirstOrDefault();
        if (token == null)
            return true;

        if (token.Equals("advanced", StringComparison.OrdinalIgnoreCase))
            return isAdvanced;

        if (token.Equals("handbook", StringComparison.OrdinalIgnoreCase))
            return !isAdvanced;

        return true;
    }

    private static bool SpellMatchesColourToken(SpellService.SpellRaw spell, string selectedToken)
    {
        var rawColour = (spell.colour ?? string.Empty).Trim();
        if (rawColour.Length == 0)
            return false;

        var normalizedRaw = NormalizeForSearch(rawColour);
        if (selectedToken.Equals("sorcorial", StringComparison.OrdinalIgnoreCase))
            return normalizedRaw.Contains("sorc", StringComparison.OrdinalIgnoreCase);

        var split = rawColour
            .Split([',', '/', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeForSearch)
            .Where(t => t.Length > 0)
            .ToList();

        if (split.Any(t => string.Equals(t, selectedToken, StringComparison.OrdinalIgnoreCase)))
            return true;

        return normalizedRaw.Contains(selectedToken, StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> ExtractEvocationFieldTokens(IEnumerable<string>? rawFields)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in rawFields ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var normalized = NormalizeForSearch(raw);
            if (EvocationFieldAliases.TryGetValue(normalized, out var aliased))
                tokens.Add(aliased);
            else
                tokens.Add(normalized);
        }

        return tokens;
    }

    private static string NormalizeMiracleSphereToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var normalized = NormalizeForSearch(raw.Trim());
        if (normalized.StartsWith("major ", StringComparison.Ordinal))
            return normalized["major ".Length..];

        if (normalized.StartsWith("minor ", StringComparison.Ordinal))
            return normalized["minor ".Length..];

        return normalized;
    }

    private static readonly Dictionary<string, string> EvocationFieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forest"] = NormalizeForSearch(EvocationFields.Forests.ToString()),
        ["river"] = NormalizeForSearch(EvocationFields.Rivers.ToString()),
        ["mountain"] = NormalizeForSearch(EvocationFields.Mountains.ToString()),
        ["desert"] = NormalizeForSearch(EvocationFields.Deserts.ToString()),
        ["city"] = NormalizeForSearch(EvocationFields.Cities.ToString()),
        ["sky"] = NormalizeForSearch(EvocationFields.Skies.ToString()),
        ["keeperofthewind"] = NormalizeForSearch(EvocationFields.KeeperOfWinds.ToString())
    };

    private static string NormalizeForSearch(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.ToLowerInvariant();
    }

    private static (MatchType MatchType, int Position, int LengthDelta) ComputeFieldMatchRank(
        string fieldValue,
        string normalizedQuery)
    {
        if (string.IsNullOrEmpty(normalizedQuery) || string.IsNullOrEmpty(fieldValue))
            return (MatchType.None, int.MaxValue, int.MaxValue);

        var normalized = NormalizeForSearch(fieldValue);
        var queryLen = normalizedQuery.Length;
        var fieldLen = normalized.Length;

        if (normalized == normalizedQuery)
            return (MatchType.Exact, 0, 0);

        if (normalized.StartsWith(normalizedQuery, StringComparison.Ordinal))
            return (MatchType.Prefix, 0, fieldLen - queryLen);

        var pos = normalized.IndexOf(normalizedQuery, StringComparison.Ordinal);
        if (pos >= 0)
            return (MatchType.Contains, pos, fieldLen - queryLen);

        return (MatchType.None, int.MaxValue, int.MaxValue);
    }

    private enum MatchType
    {
        Exact = 0,
        Prefix = 1,
        Contains = 2,
        None = 3
    }
}
