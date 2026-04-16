using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public static class DetailCardLookupService
{
    public sealed class DetailCardLookupResult
    {
        public string Key { get; init; } = string.Empty;
        public EvolutionService.AbilityResult? Ability { get; init; }
        public SpecialisationRecord? Specialisation { get; init; }

        public bool HasValue => Ability != null || Specialisation != null;
    }

    public static async Task<(string Key, SpecialisationRecord? Record)> FindSpecialisationAsync(string key)
    {
        var match = await FindByKeyAsync<SpecialisationRecord>(
            key,
            async () => await SpecialisationService.GetAllAsync());
        return (match.Key, match.Value);
    }

    public static async Task<(string Key, AbilityDefinition? Ability)> FindSpecialisationAbilityAsync(string key)
    {
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        var match = await FindByKeyAsync<AbilityDefinition>(
            key,
            () => Task.FromResult(index.AbilityReferences));
        return (match.Key, match.Value);
    }

    public static async Task<EvolutionService.AbilityResult?> ResolveAbilityAsync(string key)
    {
        var requested = (key ?? string.Empty).Trim();
        if (requested.Length == 0)
            return null;

        var byIndex = await AbilityDetailsLookupService.FindByIndexAsync(requested);
        if (byIndex != null)
            return byIndex;

        var fromSpecialisation = await FindSpecialisationAbilityAsync(requested);
        if (fromSpecialisation.Ability == null)
            return null;

        return ToAbilityResult(fromSpecialisation.Ability, fromSpecialisation.Key, requested);
    }

    public static async Task<DetailCardLookupResult?> ResolveDetailAsync(params string?[] candidates)
    {
        var normalizedCandidates = (candidates ?? Array.Empty<string?>())
            .Select(candidate => (candidate ?? string.Empty).Trim())
            .Where(candidate => candidate.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var candidate in normalizedCandidates)
        {
            var ability = await ResolveAbilityAsync(candidate);
            if (ability != null)
            {
                return new DetailCardLookupResult
                {
                    Key = candidate,
                    Ability = ability
                };
            }
        }

        foreach (var candidate in normalizedCandidates)
        {
            var specialisation = await FindSpecialisationAsync(candidate);
            if (specialisation.Record != null)
            {
                return new DetailCardLookupResult
                {
                    Key = specialisation.Key,
                    Specialisation = specialisation.Record
                };
            }
        }

        return null;
    }

    public static async Task<(string Key, TValue? Value)> FindByKeyAsync<TValue>(
        string key,
        Func<Task<IReadOnlyDictionary<string, TValue>>> loader)
        where TValue : class
    {
        var requested = (key ?? string.Empty).Trim();
        if (requested.Length == 0)
            return (string.Empty, null);

        var all = await loader();
        if (all.Count == 0)
            return (string.Empty, null);

        foreach (var entry in all)
        {
            if (!entry.Key.Equals(requested, StringComparison.OrdinalIgnoreCase))
                continue;

            return (entry.Key, entry.Value);
        }

        var normalizedRequested = NormalizeLookupKey(requested);
        if (normalizedRequested.Length == 0)
            return (string.Empty, null);

        foreach (var entry in all)
        {
            if (NormalizeLookupKey(entry.Key).Equals(normalizedRequested, StringComparison.OrdinalIgnoreCase))
                return (entry.Key, entry.Value);
        }

        var singularRequested = TrimPluralToken(normalizedRequested);
        foreach (var entry in all)
        {
            var normalizedKey = NormalizeLookupKey(entry.Key);
            if (normalizedKey.Equals(singularRequested, StringComparison.OrdinalIgnoreCase)
                || TrimPluralToken(normalizedKey).Equals(singularRequested, StringComparison.OrdinalIgnoreCase))
            {
                return (entry.Key, entry.Value);
            }
        }

        return (string.Empty, null);
    }

    public static EvolutionService.AbilityResult ToAbilityResult(
        AbilityDefinition source,
        string resolvedKey,
        string requestedKey)
    {
        var name = (source.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (resolvedKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (requestedKey ?? string.Empty).Trim();
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

    private static string NormalizeLookupKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = (value ?? string.Empty)
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();

        return new string(chars);
    }

    private static string TrimPluralToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        if (token.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && token.Length > 3)
            return $"{token[..^3]}y";

        if (token.EndsWith('s') && !token.EndsWith("ss", StringComparison.OrdinalIgnoreCase) && token.Length > 1)
            return token[..^1];

        return token;
    }
}
