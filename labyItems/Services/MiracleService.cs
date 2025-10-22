using System.Text.Json;

namespace labyItems.Services;

public static class MiracleService
{
    private class MiracRaw
    {
        public string? power { get; set; }
        public List<string>? fields { get; set; }
    }

    public record MiracEntry(string Name, int Power, IReadOnlyList<string> Fields);

    private static List<MiracEntry>? _cache;

    public static async Task<IReadOnlyList<MiracEntry>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("words_from_above/Miracs.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();
        var dict = JsonSerializer.Deserialize<Dictionary<string, MiracRaw>>(json)
                   ?? new Dictionary<string, MiracRaw>();

        _cache = dict
            .Select(kvp =>
            {
                var name = kvp.Key;
                var power = int.TryParse(kvp.Value.power, out var p) ? p : 0;
                var fields = kvp.Value.fields ?? new List<string>();
                return new MiracEntry(name, power, fields);
            })
            .OrderBy(e => e.Name)
            .ToList();

        return _cache;
    }

    public static async Task<IReadOnlyList<MiracEntry>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.Name.ToLowerInvariant().Contains(query) ||
                e.Fields.Any(f => f.ToLowerInvariant().Contains(query)))
            .ToList();
    }
}
