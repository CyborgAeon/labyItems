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
        public List<int[]>? damage { get; set; }
        public List<string>? damType { get; set; }
        public List<int[]>? MACApplies { get; set; }
        public List<int>? PACDam { get; set; }
        public string? damageOverride { get; set; }
    }

    private static List<SpellRaw>? _cache;

    public static async Task<List<SpellRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        var dbPath = ResolveDbPath();
        var dbList = LoadFromDatabase(dbPath);
        if (dbList is { Count: > 0 })
        {
            _cache = dbList;
            return _cache;
        }

        return new List<SpellRaw>();
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

    private static List<SpellRaw>? LoadFromDatabase(string? dbPath)
    {
        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            return null;

        try
        {
            using var conn = new SQLite.SQLiteConnection(dbPath, SQLite.SQLiteOpenFlags.ReadOnly);
            var rows = conn.Query<DbRow>("SELECT data_json FROM spells ORDER BY level, name;");
            var list = new List<SpellRaw>();
            foreach (var row in rows)
            {
                var e = JsonSerializer.Deserialize<SpellRaw>(row.data_json);
                if (e != null) list.Add(e);
            }
            return list;
        }
        catch
        {
            return null;
        }
    }

    private sealed class DbRow
    {
        public string data_json { get; set; } = string.Empty;
    }

    private static string? ResolveDbPath()
    {
        var appDb = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
        if (File.Exists(appDb))
        {
            return appDb;
        }

        var devCandidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "output", "laby.db"),
        };

        foreach (var candidate in devCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
