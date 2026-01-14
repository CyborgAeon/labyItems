using System.Text.Json;
using SQLite;

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
    private static readonly string? _dbPath = ResolveDbPath();

    public static async Task<IReadOnlyList<MiracRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        var dbList = LoadFromDatabase();
        if (dbList is { Count: > 0 })
        {
            _cache = dbList;
            return _cache;
        }

        throw new InvalidOperationException("Miracles DB not found or empty; ensure default.db is installed.");
    }

    private static List<MiracRaw>? LoadFromDatabase()
    {
        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
            return null;

        try
        {
            using var conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);
            var rows = conn.Query<DbRow>("SELECT data_json FROM miracles ORDER BY power, name;");
            var list = new List<MiracRaw>();
            foreach (var row in rows)
            {
                var e = JsonSerializer.Deserialize<MiracRaw>(row.data_json);
                if (e != null) list.Add(e);
            }

            return Normalize(list);
        }
        catch
        {
            return null;
        }
    }

    private static List<MiracRaw> Normalize(IEnumerable<MiracRaw> source) =>
        source
            .Select(e => new MiracRaw
            {
                power = e.power,
                name = e.name ?? string.Empty,
                description = e.description ?? string.Empty,
                sphere = e.sphere ?? string.Empty,
                isAdvanced = e.isAdvanced,
                alignment = e.alignment ?? string.Empty,
            })
            .OrderBy(e => e.power)
            .ThenBy(e => e.name)
            .ToList();

    public static async Task<IReadOnlyList<MiracRaw>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.name.ToLowerInvariant().Contains(query))
            .ToList();
    }

    private sealed class DbRow
    {
        public string data_json { get; set; } = string.Empty;
    }

    private static string? ResolveDbPath()
    {
        var appDb = Path.Combine(FileSystem.AppDataDirectory, "default.db");
        if (File.Exists(appDb))
        {
            return appDb;
        }

        var devCandidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "output", "default.db"),
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
