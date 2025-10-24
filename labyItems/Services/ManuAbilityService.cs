using System.Text.Json;

namespace labyItems.Services;

public static class ManuAbilityService
{
    private class ManuAbilityRaw
    {
        public string name { get; set; }
        public string availability { get; set; }
        public int table { get; set; }
        public int cost { get; set; }
        public bool canBuyMultiple { get; set; }
        public string description { get; set; }
    }

    public record ManuAbilityEntry(string name, string availability, int cost, int table, string description);

    private static List<ManuAbilityEntry>? _cache;

    public static async Task<IReadOnlyList<ManuAbilityEntry>> GetAllAsync()
    {
        if (_cache != null) return _cache;
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dict = JsonSerializer.Deserialize<Dictionary<string, ManuAbilityRaw>>(MakesAbilitiesJson.Json, opts)
                   ?? new Dictionary<string, ManuAbilityRaw>();

        _cache = dict
            .Select(kvp =>
            {
                return new ManuAbilityEntry(kvp.Key, kvp.Value.availability, kvp.Value.cost, kvp.Value.table, kvp.Value.description);
            })
            .OrderBy(e => e.name)
            .ToList();

        return _cache;
    }

    public static async Task<IReadOnlyList<ManuAbilityEntry>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.name.ToLowerInvariant().Contains(query))
            .ToList();
    }
}
