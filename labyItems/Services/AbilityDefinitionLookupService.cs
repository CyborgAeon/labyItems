using System.Text.Json;
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
        if (_lookup is not null)
            return _lookup;

        await LookupLock.WaitAsync();
        try
        {
            if (_lookup is not null)
                return _lookup;

            var map = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);

            // Primary source: normalized specialisation index.
            var index = await SpecialisationDefinitionRepository.GetIndexAsync();
            foreach (var entry in index.AbilityReferences)
                AddLookupEntries(map, entry.Key, entry.Value);

            // Secondary source: raw abilities.json (captures entries that may not be referenced elsewhere yet).
            await AddRawAbilityEntriesAsync(map);

            _lookup = map;
            return _lookup;
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

    private static async Task AddRawAbilityEntriesAsync(IDictionary<string, AbilityDefinition> map)
    {
        var json = await ServiceHelper.ReadPackageTextAsync("specialisation/abilities.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        using var document = JsonDocument.Parse(json);
        if (!TryGetProperty(document.RootElement, "abilities", out var abilitiesElement)
            || abilitiesElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var entry in abilitiesElement.EnumerateObject())
        {
            AbilityDefinition? definition = null;
            try
            {
                definition = JsonSerializer.Deserialize<AbilityDefinition>(entry.Value.GetRawText());
            }
            catch
            {
                // Ignore malformed entries and continue.
            }

            if (definition == null)
                continue;

            AddLookupEntries(map, entry.Name, definition);
        }
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

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            value = property.Value;
            return true;
        }

        return false;
    }
}
