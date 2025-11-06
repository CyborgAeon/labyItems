using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Pages;

namespace labyItems.Services;

public static class AbilityHelper
{
    public static bool IsImmunity(this string fromIndex)
    {
        if (fromIndex.Contains("Immunity!")) return true;
        else return false;
    }
}

public static class GeneralService
{
    // Raw JSON shape in each file
    private sealed class TableRaw
    {
        [JsonPropertyName("available")] public string? Available { get; set; }
        [JsonPropertyName("index")] public string Index { get; set; }
        [JsonPropertyName("desc")] public string? Desc { get; set; }
        [JsonPropertyName("cost")] public string? Cost { get; set; }
    }

    private static IReadOnlyList<General.Result>? _cache;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string BasePath = "evolution_classes";
    private const int TableCount = 12;

    public static async Task<IReadOnlyList<General.Result>> GetAllAsync()
    {
        if (_cache is not null) return _cache;

        var list = new List<General.Result>();

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
                    continue;
                list.Add(new General.Result { Index = idx, Description = desc, Cost = cost, Table = t, IsImmunity = item.Index.IsImmunity() });
            }
        }

        _cache = list;
        return _cache;
    }

    public static async Task<IReadOnlyList<General.Result>> SearchByIndexAsync(string? query, int? table = null)
    {
        var all = await GetAllAsync();
        var q = (query ?? "").Trim();

        IEnumerable<General.Result> source = all;
        if (table is { } t && t >= 1 && t <= TableCount)
            source = source.Where(e => e.Table == t);

        if (string.IsNullOrWhiteSpace(q))
            return source.ToList();

        q = q.ToLowerInvariant();
        return source.Where(e => e.Index.ToLowerInvariant().Contains(q)).ToList();
    }
    
    public static void InvalidateCache() => _cache = null;

    private static int TryParseInt(string? s)
        => int.TryParse((s ?? "").Trim(), out var n) ? n : 0;
}
