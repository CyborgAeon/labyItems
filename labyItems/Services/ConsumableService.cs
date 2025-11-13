using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using labyItems.Pages.Configs;
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

    // Must be static because the class and methods are static
    private static List<GenericRaw>? _cache;

    public static async Task<List<GenericRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("grimoire/new_standard.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();

        _cache = JsonSerializer.Deserialize<List<GenericRaw>>(json)
                 ?? new List<GenericRaw>();

        return _cache;
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