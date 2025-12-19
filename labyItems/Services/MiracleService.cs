using System.Text.Json;
using labyItems.Services.Helpers;

namespace labyItems.Services;

public static class MiracleService
{
    public sealed record MiracRaw
    {
        public int power { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string sphere { get; set; }
        public bool isAdvanced { get; set; }
        public string alignment { get; set; }
    }
    private static IndexedCache<MiracRaw>? _indexedCache;

    private static string NormalizeKey(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    public static async Task<IReadOnlyList<MiracRaw>> GetAllAsync()
    {
        var cache = await GetIndexedCacheAsync();
        return cache.Items.Select(x => x.Item).ToList();
    }

    private static async Task<IndexedCache<MiracRaw>> GetIndexedCacheAsync()
    {
        if (_indexedCache != null) return _indexedCache;

        using var s = await FileSystem.OpenAppPackageFileAsync("words_from_above/miracles.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();
        var miracles = JsonSerializer.Deserialize<List<MiracRaw>>(json)
            ?? new List<MiracRaw>();

        var sorted = miracles
            .OrderBy(e => e.power)
            .Take(50)
            .ToList();

        var exactMatches = new Dictionary<string, MiracRaw>();
        var indexed = new List<IndexedCache<MiracRaw>.IndexedItem>();

        foreach (var miracle in sorted)
        {
            var normalized = NormalizeKey(miracle.name);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                exactMatches[normalized] = miracle;
                indexed.Add(new IndexedCache<MiracRaw>.IndexedItem(normalized, miracle));
            }
        }

        _indexedCache = new IndexedCache<MiracRaw>
        {
            ExactMatches = exactMatches,
            Items = indexed
        };

        return _indexedCache;
    }

    public static async Task<IReadOnlyList<MiracRaw>> SearchAsync(string query)
    {
        var cache = await GetIndexedCacheAsync();
        return cache.SearchByContains(query);
    }
}
