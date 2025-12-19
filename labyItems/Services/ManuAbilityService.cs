using System.Text.Json;
using labyItems.Services.Helpers;

namespace labyItems.Services;

public static class ManuAbilityService
{
    private class ManuAbilityRaw
    {
        public string name { get; set; }
        public string availability { get; set; }
        public int table { get; set; }
        public int cost { get; set; }
        public bool canBuyMultiple { get; set; }
        public string description { get; set; }
    }

    public record ManuAbilityEntry(string name, string availability, int cost, int table, string description);

    private static IndexedCache<ManuAbilityEntry>? _indexedCache;

    private static string NormalizeKey(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    public static async Task<IReadOnlyList<ManuAbilityEntry>> GetAllAsync()
    {
        var cache = await GetIndexedCacheAsync();
        return cache.Items.Select(x => x.Item).ToList();
    }

    private static async Task<IndexedCache<ManuAbilityEntry>> GetIndexedCacheAsync()
    {
        if (_indexedCache != null) return _indexedCache;
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dict = JsonSerializer.Deserialize<Dictionary<string, ManuAbilityRaw>>(MakesAbilitiesJson.Json, opts)
                   ?? new Dictionary<string, ManuAbilityRaw>();

        var entries = dict
            .Select(kvp => new ManuAbilityEntry(kvp.Key, kvp.Value.availability, kvp.Value.cost, kvp.Value.table, kvp.Value.description))
            .OrderBy(e => e.name)
            .ToList();

        var exactMatches = new Dictionary<string, ManuAbilityEntry>();
        var indexed = new List<IndexedCache<ManuAbilityEntry>.IndexedItem>();

        foreach (var entry in entries)
        {
            var normalized = NormalizeKey(entry.name);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                exactMatches[normalized] = entry;
                indexed.Add(new IndexedCache<ManuAbilityEntry>.IndexedItem(normalized, entry));
            }
        }

        _indexedCache = new IndexedCache<ManuAbilityEntry>
        {
            ExactMatches = exactMatches,
            Items = indexed
        };

        return _indexedCache;
    }

    public static async Task<IReadOnlyList<ManuAbilityEntry>> SearchAsync(string query)
    {
        var cache = await GetIndexedCacheAsync();
        var results = cache.SearchByContains(query);
        return results.Take(6).ToList();
    }
}
