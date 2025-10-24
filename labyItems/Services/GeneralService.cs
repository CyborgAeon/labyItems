using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Services;

public static class GeneralService
{
    // Public DTO used by the app
    public sealed record TableEntry(
        int Table,             // table number: 1..12
        string Index,          // e.g. "Rebirth"
        string Description,    // desc
        int Cost,              // parsed numeric cost
        string Available       // e.g. "ALL"
    );

    // Raw JSON shape in each file
    private sealed class TableRaw
    {
        [JsonPropertyName("available")] public string? Available { get; set; }
        [JsonPropertyName("index")] public string? Index { get; set; }
        [JsonPropertyName("desc")] public string? Desc { get; set; }
        [JsonPropertyName("cost")] public string? Cost { get; set; }
    }

    private static IReadOnlyList<TableEntry>? _cache;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string BasePath = "evolution_classes";
    private const int TableCount = 12;

    public static async Task<IReadOnlyList<TableEntry>> GetAllAsync()
    {
        if (_cache is not null) return _cache;

        var list = new List<TableEntry>(256);

        for (int t = 1; t <= TableCount; t++)
        {
            var file = $"{BasePath}/table_{t}.json";
            using var s = await FileSystem.OpenAppPackageFileAsync(file);
            using var r = new StreamReader(s);
            var json = await r.ReadToEndAsync();

            var raw = JsonSerializer.Deserialize<List<TableRaw>>(json, _json) ?? new();
            foreach (var item in raw)
            {
                var idx = (item.Index ?? "").Trim();
                var desc = (item.Desc ?? "").Trim();
                var avail = (item.Available ?? "").Trim();
                var cost = TryParseInt(item.Cost);

                if (string.IsNullOrWhiteSpace(idx))
                    continue; // skip malformed

                list.Add(new TableEntry(t, idx, desc, cost, avail));
            }
        }

        _cache = list
            .OrderBy(e => e.Table)
            .ThenBy(e => e.Index, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        return _cache;
    }

    /// <summary>Search by index (contains, case-insensitive). If table is supplied, restrict to that table.</summary>
    public static async Task<IReadOnlyList<TableEntry>> SearchByIndexAsync(string? query, int? table = null)
    {
        var all = await GetAllAsync();
        var q = (query ?? "").Trim();

        IEnumerable<TableEntry> source = all;
        if (table is { } t && t >= 1 && t <= TableCount)
            source = source.Where(e => e.Table == t);

        if (string.IsNullOrWhiteSpace(q))
            return source.ToList();

        q = q.ToLowerInvariant();
        return source.Where(e => e.Index.ToLowerInvariant().Contains(q)).ToList();
    }

    /// <summary>Get all entries from a specific table number (1..12).</summary>
    public static async Task<IReadOnlyList<TableEntry>> GetByTableAsync(int table)
    {
        var all = await GetAllAsync();
        return all.Where(e => e.Table == table).ToList();
    }

    /// <summary>Try get exact match on index (optionally within a table).</summary>
    public static async Task<TableEntry?> TryGetByIndexAsync(string index, int? table = null)
    {
        var all = await GetAllAsync();
        var q = index.Trim();

        IEnumerable<TableEntry> source = all;
        if (table is { } t && t >= 1 && t <= TableCount)
            source = source.Where(e => e.Table == t);

        return source.FirstOrDefault(e => string.Equals(e.Index, q, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Clear the in-memory cache (e.g., if you hot-swap files during dev).</summary>
    public static void InvalidateCache() => _cache = null;

    private static int TryParseInt(string? s)
        => int.TryParse((s ?? "").Trim(), out var n) ? n : 0;
}
