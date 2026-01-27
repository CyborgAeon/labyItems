using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class DruidEvocationService
{
    public sealed record EvocRaw
    {
        public string name { get; set; } = string.Empty;
        public int power { get; set; }
        public List<string> fields { get; set; } = new();
        public bool isAdvanced { get; set; }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static List<EvocRaw>? _cache;

    public static async Task<IReadOnlyList<EvocRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("druids_way/evocs.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();

        var list = JsonSerializer.Deserialize<List<EvocRaw>>(json, _jsonOptions)
                   ?? new List<EvocRaw>();

        _cache = Normalize(list);
        return _cache;
    }

    private static List<EvocRaw> Normalize(IEnumerable<EvocRaw> source)
        => source
            .Select(e => new EvocRaw
            {
                name = e.name ?? string.Empty,
                power = e.power,
                fields = (e.fields ?? new List<string>())
                    .Select(f => (f ?? string.Empty).Trim())
                    .Where(f => f.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                isAdvanced = e.isAdvanced
            })
            .OrderBy(e => e.power)
            .ThenBy(e => e.name)
            .ToList();
}
