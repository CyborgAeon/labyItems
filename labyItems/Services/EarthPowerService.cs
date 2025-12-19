using System.Text.Json;
using labyItems.Services.Helpers;

namespace labyItems.Services;

public static class EarthPowerService
{
    public class EvocRaw
    {
        public string name { get; set; }
        public int power { get; set; }
        public string range { get; set; }
        public string duration { get; set; }
        public string verbal { get; set; }
        public List<string>? fields { get; set; }
        public string description { get; set; }
        public bool isAdvanced { get; set; }
    }
    private static IndexedCache<EvocRaw>? _indexedCache;

    private static string NormalizeKey(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    public static async Task<IReadOnlyList<EvocRaw>> GetAllAsync()
    {
        var cache = await GetIndexedCacheAsync();
        return cache.Items.Select(x => x.Item).ToList();
    }

    private static async Task<IndexedCache<EvocRaw>> GetIndexedCacheAsync()
    {
        if (_indexedCache != null) return _indexedCache;

        using var s = await FileSystem.OpenAppPackageFileAsync("druids_way/evocs.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();
        var evocs = JsonSerializer.Deserialize<List<EvocRaw>>(json)
                   ?? new List<EvocRaw>();

        var exactMatches = new Dictionary<string, EvocRaw>();
        var indexed = new List<IndexedCache<EvocRaw>.IndexedItem>();

        foreach (var evoc in evocs)
        {
            var normalized = NormalizeKey(evoc.name);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                exactMatches[normalized] = evoc;
                indexed.Add(new IndexedCache<EvocRaw>.IndexedItem(normalized, evoc));
            }
        }

        _indexedCache = new IndexedCache<EvocRaw>
        {
            ExactMatches = exactMatches,
            Items = indexed
        };

        return _indexedCache;
    }

    public static async Task<IReadOnlyList<EvocRaw>> SearchAsync(string query)
    {
        var cache = await GetIndexedCacheAsync();
        var results = cache.SearchByContains(query);
        return results.Take(20).ToList();
    }
}
