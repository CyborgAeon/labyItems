using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public static class DetailCardLookupService
{
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
