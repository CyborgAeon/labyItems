using System.Text.Json;

namespace labyItems.Services;

public static class SpellService
{
    private class SpellRaw
    {
        public string name { get; set; }
        public int level { get; set; }
        public string colour { get; set; }
        public string range { get; set; }
        public string duration { get; set; }
        public string verbal { get; set; }
        public string description { get; set; }
    }

    public record SpellEntry(string Name, int Power, string Colour);

    private static List<SpellEntry>? _cache;

    public static async Task<IReadOnlyList<SpellEntry>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("grimoire/standard.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();
        var dict = JsonSerializer.Deserialize<Dictionary<string, SpellRaw>>(json)
                   ?? new Dictionary<string, SpellRaw>();

        _cache = dict
            .Select(kvp =>
            {
                var name = kvp.Key;
                var power = kvp.Value.level;
                var colour = kvp.Value.colour;
                return new SpellEntry(name, power, colour);
            })
            .OrderBy(e => e.Name)
            .ToList();

        return _cache;
    }

    public static async Task<IReadOnlyList<SpellEntry>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.Name.ToLowerInvariant().Contains(query))
            .ToList();
    }
}
