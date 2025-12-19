using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using labyItems.Pages.Configs;
using labyItems.Services.Helpers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class ConsumableService
{
    public class GenericRaw
    {
        public string name { get; set; }
        public int cost { get; set; }
        public bool isAdvanced { get; set; }
    }

    private static IndexedCache<GenericRaw>? _indexedCache;

    private static string NormalizeKey(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    public static async Task<List<GenericRaw>> GetAllAsync()
    {
        var cache = await GetIndexedCacheAsync();
        return cache.Items.Select(x => x.Item).ToList();
    }

    private static async Task<IndexedCache<GenericRaw>> GetIndexedCacheAsync()
    {
        if (_indexedCache != null) return _indexedCache;

        using var s = await FileSystem.OpenAppPackageFileAsync("grimoire/new_standard.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();

        var consumables = JsonSerializer.Deserialize<List<GenericRaw>>(json)
                 ?? new List<GenericRaw>();

        var exactMatches = new Dictionary<string, GenericRaw>();
        var indexed = new List<IndexedCache<GenericRaw>.IndexedItem>();

        foreach (var consumable in consumables)
        {
            var normalized = NormalizeKey(consumable.name);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                exactMatches[normalized] = consumable;
                indexed.Add(new IndexedCache<GenericRaw>.IndexedItem(normalized, consumable));
            }
        }

        _indexedCache = new IndexedCache<GenericRaw>
        {
            ExactMatches = exactMatches,
            Items = indexed
        };

        return _indexedCache;
    }

    public static Task<ConsumableEntry?> PickAsync(INavigation nav, ConsumableType type) =>
        type switch
        {
            // ConsumableType.BatchOfPotions    => BatchOfPotionsPickerPage.PickAsync(nav),
            // ConsumableType.MagicalScroll     => MagicalScrollPickerPage.PickAsync(nav),
            // ConsumableType.SpiritualScroll   => SpiritualScrollPickerPage.PickAsync(nav),
            // ConsumableType.DruidicTalisman   => DruidicTalismanPickerPage.PickAsync(nav),
            // ConsumableType.NeuronicShard     => NeuronicShardPickerPage.PickAsync(nav),
            _                                => Task.FromResult<ConsumableEntry?>(null)
        };
}