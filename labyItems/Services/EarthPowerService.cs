using System.Text.Json;

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
    private static List<EvocRaw>? _cache;

    public static async Task<IReadOnlyList<EvocRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("druids_way/evocs.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();
        _cache = JsonSerializer.Deserialize<List<EvocRaw>>(json)
                   ?? new List<EvocRaw>();

        return _cache;
    }

    public static async Task<IReadOnlyList<EvocRaw>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.name.ToLowerInvariant().Contains(query))
                .Take(20)
                .ToList();
    }
}
