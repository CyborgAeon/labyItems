using System.Text.RegularExpressions;

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
                foreach (var equivalent in EvolutionService.GetEquivalentAbilityNames(ability.Index))
                {
                    var key = NormalizeKey(equivalent);
                    if (key.Length == 0 || map.ContainsKey(key))
                        continue;

                    map[key] = ability;
                }
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
}
