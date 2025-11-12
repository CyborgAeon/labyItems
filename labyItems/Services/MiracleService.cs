using System.Text.Json;

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
    private static List<MiracRaw>? _cache;

    public static async Task<IReadOnlyList<MiracRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("words_from_above/miracles.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();
        var dict = JsonSerializer.Deserialize<List<MiracRaw>>(json)
            ?? new List<MiracRaw>();

        _cache = dict
            .Select(e => new MiracRaw
            {
                power = e.power,
                name = e.name,
                description = e.description,
                sphere = e.sphere,
                isAdvanced = e.isAdvanced,
                alignment = e.alignment,
            })
            .OrderBy(e => e.power)
            .Take(50)
            .ToList();

        return _cache;
    }

    public static async Task<IReadOnlyList<MiracRaw>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.name.ToLowerInvariant().Contains(query))
            .ToList();
    }
}
