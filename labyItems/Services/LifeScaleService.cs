using System.Text.Json;
using System.Text.Json.Serialization;
using SQLite;

namespace labyItems.Services;

public static class LifeScaleService
{
    public readonly record struct LifeScaleLevel(int Life, int Spirit);

    public sealed record LifeScaleEntry(string Race, string ClassName, IReadOnlyList<LifeScaleLevel> Levels);

    private const string PackagedFileName = "people/lifescales.json";
    private static readonly string? _dbPath = ResolveDbPath();
    private static List<LifeScaleEntry>? _cache;

    public static async Task<IReadOnlyList<LifeScaleEntry>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        var dbList = LoadFromDatabase();
        if (dbList is { Count: > 0 })
        {
            _cache = dbList;
            return _cache;
        }

        var fileList = await LoadFromJsonAsync();
        if (fileList.Count == 0)
        {
            throw new InvalidOperationException(
                "Lifescales data not found; ensure lifescales.json is packaged or the database table exists.");
        }

        _cache = fileList;
        return _cache;
    }

    private static List<LifeScaleEntry>? LoadFromDatabase()
    {
        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
            return null;

        try
        {
            using var conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);
            var rows = conn.Query<DbRow>("SELECT data_json FROM lifescales ORDER BY race, class;");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = new List<LifeScaleEntry>();

            foreach (var row in rows)
            {
                var e = JsonSerializer.Deserialize<DbLifeScale>(row.data_json, options);
                if (e != null)
                    list.Add(Convert(e));
            }

            return Normalize(list);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<List<LifeScaleEntry>> LoadFromJsonAsync()
    {
        try
        {
            using var s = await FileSystem.OpenAppPackageFileAsync(PackagedFileName);
            using var r = new StreamReader(s);
            var json = await r.ReadToEndAsync();
            return Normalize(ParseNestedJson(json));
        }
        catch
        {
            // fall back to dev-time path
        }

        var devPath = ResolveDevJsonPath();
        if (!string.IsNullOrWhiteSpace(devPath) && File.Exists(devPath))
        {
            var json = File.ReadAllText(devPath);
            return Normalize(ParseNestedJson(json));
        }

        return new List<LifeScaleEntry>();
    }

    private static string? ResolveDevJsonPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Raw", "people", "lifescales.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "labyItems", "Resources", "Raw", "people", "lifescales.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static List<LifeScaleEntry> ParseNestedJson(string json)
    {
        var nested = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int[][]>>>(json);
        if (nested is null)
            return new List<LifeScaleEntry>();

        var list = new List<LifeScaleEntry>();
        foreach (var (race, classMap) in nested)
        {
            if (classMap is null) continue;

            foreach (var (className, scales) in classMap)
            {
                var levels = (scales ?? Array.Empty<int[]>())
                    .Select(pair =>
                    {
                        int life = pair.Length > 0 ? pair[0] : 0;
                        int spirit = pair.Length > 1 ? pair[1] : 0;
                        return new LifeScaleLevel(life, spirit);
                    })
                    .ToList();

                list.Add(new LifeScaleEntry(race, className, levels));
            }
        }

        return list;
    }

    private static LifeScaleEntry Convert(DbLifeScale raw)
    {
        var levels = (raw.levels ?? new List<int[]>())
            .Select(pair =>
            {
                int life = pair is { Length: > 0 } ? pair[0] : 0;
                int spirit = pair is { Length: > 1 } ? pair[1] : 0;
                return new LifeScaleLevel(life, spirit);
            })
            .ToList();

        return new LifeScaleEntry(raw.race ?? string.Empty, raw.@class ?? raw.className ?? string.Empty, levels);
    }

    private static List<LifeScaleEntry> Normalize(IEnumerable<LifeScaleEntry> source) =>
        source
            .Select(e => new LifeScaleEntry(
                e.Race?.Trim() ?? string.Empty,
                e.ClassName?.Trim() ?? string.Empty,
                e.Levels?.Select(l => new LifeScaleLevel(l.Life, l.Spirit)).ToList() ?? new List<LifeScaleLevel>()))
            .Where(e => !string.IsNullOrWhiteSpace(e.Race) && !string.IsNullOrWhiteSpace(e.ClassName))
            .OrderBy(e => e.Race, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ClassName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed class DbRow
    {
        public string data_json { get; set; } = string.Empty;
    }

    private sealed class DbLifeScale
    {
        public string race { get; set; } = string.Empty;

        [JsonPropertyName("class")]
        public string @class { get; set; } = string.Empty;

        public string className { get; set; } = string.Empty;
        public List<int[]>? levels { get; set; }
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
