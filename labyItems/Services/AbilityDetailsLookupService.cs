using System.Text.RegularExpressions;
using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public static class AbilityDetailsLookupService
{
    private static readonly SemaphoreSlim LookupLock = new(1, 1);
    private static readonly Regex TrailingParentheticalRegex = new(@"\s*\([^)]*\)\s*$", RegexOptions.Compiled);
    private static IReadOnlyDictionary<string, EvolutionService.AbilityResult>? _lookup;

    public static async Task<IReadOnlyDictionary<string, EvolutionService.AbilityResult>> GetLookupAsync()
    {
        if (_lookup is not null)
            return _lookup;

        await LookupLock.WaitAsync();
        try
        {
            if (_lookup is not null)
                return _lookup;

            var abilities = await EvolutionService.GetAllAbilitiesAsync();
            var map = new Dictionary<string, EvolutionService.AbilityResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var ability in abilities)
            {
                AddLookupEntries(map, ability, ability.Index);
            }

            var specialisationIndex = await SpecialisationDefinitionRepository.GetIndexAsync();
            foreach (var entry in specialisationIndex.AbilityReferences)
            {
                var definition = entry.Value;
                var candidates = BuildCandidateTerms(entry.Key, definition);
                var resolved = ResolveExistingAbility(map, candidates)
                    ?? ToAbilityResult(definition, entry.Key);

                AddLookupEntries(map, resolved, candidates);
            }

            _lookup = map;
            return _lookup;
        }
        finally
        {
            LookupLock.Release();
        }
    }

    public static async Task<EvolutionService.AbilityResult?> FindByIndexAsync(string? rawIndex)
    {
        var lookup = await GetLookupAsync();
        return FindByIndex(lookup, rawIndex);
    }

    public static EvolutionService.AbilityResult? FindByIndex(
        IReadOnlyDictionary<string, EvolutionService.AbilityResult> lookup,
        string? rawIndex)
    {
        var current = (rawIndex ?? string.Empty).Trim();
        if (current.Length == 0)
            return null;

        while (current.Length > 0)
        {
            foreach (var equivalent in EvolutionService.GetEquivalentAbilityNames(current))
            {
                var key = NormalizeKey(equivalent);
                if (key.Length > 0 && lookup.TryGetValue(key, out var ability))
                    return ability;
            }

            var stripped = StripTrailingParenthetical(current);
            if (stripped.Length == current.Length)
                break;

            current = stripped;
        }

        return null;
    }

    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .ToArray();
        return new string(chars).ToLowerInvariant();
    }

    public static void InvalidateCache()
    {
        _lookup = null;
    }

    private static string StripTrailingParenthetical(string value)
    {
        var text = value ?? string.Empty;
        while (text.Length > 0)
        {
            var stripped = TrailingParentheticalRegex.Replace(text, string.Empty).Trim();
            if (stripped.Length == text.Length)
                return stripped;
            text = stripped;
        }

        return text;
    }

    private static IEnumerable<string> BuildCandidateTerms(string sourceKey, AbilityDefinition definition)
    {
        yield return sourceKey;
        yield return definition.Key ?? string.Empty;
        yield return definition.Name ?? string.Empty;
        yield return definition.UpdateKey ?? string.Empty;
        yield return definition.BattleboardNameOverride ?? string.Empty;
        yield return definition.OverwriteKey ?? string.Empty;
    }

    private static EvolutionService.AbilityResult? ResolveExistingAbility(
        IReadOnlyDictionary<string, EvolutionService.AbilityResult> lookup,
        IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            foreach (var equivalent in EvolutionService.GetEquivalentAbilityNames(candidate))
            {
                var key = NormalizeKey(equivalent);
                if (key.Length == 0)
                    continue;

                if (lookup.TryGetValue(key, out var match))
                    return match;
            }
        }

        return null;
    }

    private static EvolutionService.AbilityResult ToAbilityResult(AbilityDefinition source, string sourceKey)
    {
        var name = (source.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (source.Key ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (sourceKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = "Ability";

        return new EvolutionService.AbilityResult
        {
            Index = name,
            Description = source.Effect ?? string.Empty,
            Cost = 0,
            Table = 0,
            Available = source.Source ?? "ALL",
            CanBuyMultiple = false,
            PreReqs = source.PreReqs is { Count: > 0 } preReqs
                ? preReqs
                : Array.Empty<string>(),
            MaxAvailable = source.Count
        };
    }

    private static void AddLookupEntries(
        IDictionary<string, EvolutionService.AbilityResult> map,
        EvolutionService.AbilityResult ability,
        IEnumerable<string> terms)
    {
        foreach (var term in terms)
            AddLookupEntries(map, ability, term);
    }

    private static void AddLookupEntries(
        IDictionary<string, EvolutionService.AbilityResult> map,
        EvolutionService.AbilityResult ability,
        params string[] terms)
    {
        foreach (var term in terms)
        {
            foreach (var equivalent in EvolutionService.GetEquivalentAbilityNames(term))
            {
                var key = NormalizeKey(equivalent);
                if (key.Length == 0 || map.ContainsKey(key))
                    continue;

                map[key] = ability;
            }
        }
    }
}
