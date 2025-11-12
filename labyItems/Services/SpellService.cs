using System.Text.Json;

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

    private static List<SpellRaw>? _cache;

    public static async Task<List<SpellRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("grimoire/new_standard.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();
        _cache = JsonSerializer.Deserialize<List<SpellRaw>>(json)
                   ?? new List<SpellRaw>();
        return _cache;
    }

    public static async Task<List<SpellRaw>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.name.ToLowerInvariant().Contains(query))
            .ToList();
    }
}
