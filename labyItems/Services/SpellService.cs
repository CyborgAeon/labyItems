using System.Text.Json;
using labyItems.Services.Helpers;

namespace labyItems.Services;

public static class SpellService
{
    public class SpellRaw
    {
        public string name { get; set; } = string.Empty;
        public int level { get; set; } = 0;
        public string colour { get; set; } = string.Empty;
        public string range { get; set; } = string.Empty;
        public string duration { get; set; } = string.Empty;
        public string verbal { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public bool? isAdvanced { get; set; } = false;
    }

    private static IndexedCache<SpellRaw>? _indexedCache;

    private static string NormalizeKey(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    public static async Task<List<SpellRaw>> GetAllAsync()
    {
        var cache = await GetIndexedCacheAsync();
        return cache.Items.Select(x => x.Item).ToList();
    }

    private static async Task<IndexedCache<SpellRaw>> GetIndexedCacheAsync()
    {
        if (_indexedCache != null) return _indexedCache;

        using var s = await FileSystem.OpenAppPackageFileAsync("grimoire/new_standard.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();
        var spells = JsonSerializer.Deserialize<List<SpellRaw>>(json)
                   ?? new List<SpellRaw>();

        var exactMatches = new Dictionary<string, SpellRaw>();
        var indexed = new List<IndexedCache<SpellRaw>.IndexedItem>();

        foreach (var spell in spells)
        {
            var normalized = NormalizeKey(spell.name);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                exactMatches[normalized] = spell;
                indexed.Add(new IndexedCache<SpellRaw>.IndexedItem(normalized, spell));
            }
        }

        _indexedCache = new IndexedCache<SpellRaw>
        {
            ExactMatches = exactMatches,
            Items = indexed
        };

        return _indexedCache;
    }

    public static async Task<List<SpellRaw>> SearchAsync(string query)
    {
        var cache = await GetIndexedCacheAsync();
        return cache.SearchByContains(query);
    }
}
