using System.Text.Json;

namespace labyItems.Services;

public static class EarthPowerService
{
    private class EvocRaw
    {
        public string? power { get; set; }
        public List<string>? fields { get; set; }
    }

    public record EvocEntry(string Name, int Power, IReadOnlyList<string> Fields);

    private static List<EvocEntry>? _cache;

    public static async Task<IReadOnlyList<EvocEntry>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("druids_way/evocs.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();
        var dict = JsonSerializer.Deserialize<Dictionary<string, EvocRaw>>(json)
                   ?? new Dictionary<string, EvocRaw>();

        _cache = dict
            .Select(kvp =>
            {
                var name = kvp.Key;
                var power = int.TryParse(kvp.Value.power, out var p) ? p : 0;
                var fields = kvp.Value.fields ?? new List<string>();
                return new EvocEntry(name, power, fields);
            })
            .OrderBy(e => e.Name)
            .ToList();

        return _cache;
    }

    public static async Task<IReadOnlyList<EvocEntry>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.Name.ToLowerInvariant().Contains(query) ||
                e.Fields.Any(f => f.ToLowerInvariant().Contains(query)))
                .Take(6)
            .ToList();
    }
}
