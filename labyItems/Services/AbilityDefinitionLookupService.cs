using labyItems.Models.Abilities;
using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public static class AbilityDefinitionLookupService
{
    private static readonly SemaphoreSlim LookupLock = new(1, 1);
    private static IReadOnlyDictionary<string, AbilityDefinition>? _lookup;

    public static async Task<IReadOnlyDictionary<string, AbilityDefinition>> GetLookupAsync()
    {
        var accountCacheVersion = labyItems.Services.LabyrintheSession.Shared.Version;
        if (_lookup is not null)
            return _lookup ?? throw new InvalidOperationException("The account cache changed. Please retry.");

        await LookupLock.WaitAsync();
        try
        {
            if (_lookup is not null)
                return _lookup ?? throw new InvalidOperationException("The account cache changed. Please retry.");

            var map = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);

            var index = await SpecialisationDefinitionRepository.GetIndexAsync();
            foreach (var entry in index.AbilityReferences)
                AddLookupEntries(map, entry.Key, entry.Value);

            labyItems.Services.LabyrintheSession.Shared.Publish(accountCacheVersion, () => _lookup = map);
            return _lookup ?? throw new InvalidOperationException("The account cache changed. Please retry.");
        }
        finally
        {
            LookupLock.Release();
        }
    }

    public static async Task<AbilityDefinition?> FindAsync(string? keyOrName)
    {
        var lookup = await GetLookupAsync();
        return Find(lookup, keyOrName);
    }

    public static AbilityDefinition? Find(
        IReadOnlyDictionary<string, AbilityDefinition> lookup,
        string? keyOrName)
    {
        var normalized = NormalizeKey(keyOrName);
        if (normalized.Length == 0)
            return null;

        return lookup.TryGetValue(normalized, out var definition)
            ? definition
            : null;
    }

    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    public static void InvalidateCache()
    {
        _lookup = null;
    }

    private static void AddLookupEntries(
        IDictionary<string, AbilityDefinition> map,
        string sourceKey,
        AbilityDefinition definition)
    {
        if (definition == null)
            return;

        var canonicalKey = AbilityKey.Build(definition);
        if (!string.IsNullOrWhiteSpace(canonicalKey))
            AddEntry(map, canonicalKey, definition);

        AddEntry(map, sourceKey, definition);
        AddEntry(map, definition.Key, definition);
        AddEntry(map, definition.AbilityRef, definition);
        AddEntry(map, definition.Name, definition);
        AddEntry(map, definition.UpdateKey, definition);
        AddEntry(map, definition.BattleboardNameOverride, definition);
        AddEntry(map, definition.OverwriteKey, definition);
    }

    private static void AddEntry(
        IDictionary<string, AbilityDefinition> map,
        string? key,
        AbilityDefinition definition)
    {
        var normalized = NormalizeKey(key);
        if (normalized.Length == 0 || map.ContainsKey(normalized))
            return;

        map[normalized] = definition;
    }
}
