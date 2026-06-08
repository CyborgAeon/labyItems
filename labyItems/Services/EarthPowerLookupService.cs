using System.Text.Json;
using SQLite;

namespace labyItems.Services;

public sealed class EarthPowerLookupService : ILookupService
{
    public Task<IReadOnlyList<LookupItem>> GetAllAsync()
        => SearchAsync(query: "");

    public async Task<IReadOnlyList<LookupItem>> SearchAsync(string query)
    {
        var hits = await EarthPowerService.SearchAsync(query, includeAdvanced: true, maxPower: null);
        return hits
            .Select(e => new LookupItem(Key: e.name, Display: $"{e.name} (P{e.power})"))
            .ToList();
    }
}


public static class EarthPowerService
{
    public class EvocRaw
    {
        public string name { get; set; } = string.Empty;
        public int power { get; set; }
        public string range { get; set; } = string.Empty;
        public string duration { get; set; } = string.Empty;
        public string verbal { get; set; } = string.Empty;
        public List<string>? fields { get; set; }
        public string description { get; set; } = string.Empty;
        public bool isAdvanced { get; set; }
    }

    private static string? _dbPath;

    public static bool HasDatabase => !string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath);

    static EarthPowerService()
    {
        var appDb = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
        if (File.Exists(appDb))
        {
            _dbPath = appDb;
            return;
        }

        var devCandidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "output", "laby.db")
        };
        foreach (var candidate in devCandidates)
        {
            if (File.Exists(candidate))
            {
                _dbPath = candidate;
                return;
            }
        }

        _dbPath = null;
    }

    public static async Task<IReadOnlyList<EvocRaw>> GetAllAsync()
    {
        if (!string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath))
        {
            using var conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);

            var rows = conn.Query<DbRow>("SELECT data_json FROM evocs ORDER BY name;");
            var list = new List<EvocRaw>();
            foreach (var row in rows)
            {
                var e = JsonSerializer.Deserialize<EvocRaw>(row.data_json);
                if (e != null) list.Add(e);
            }

            return list;
        }

        return await LoadFromPackagedJsonAsync();
    }

    public static async Task<IReadOnlyList<EvocRaw>> SearchAsync(string query, bool includeAdvanced = true, int? maxPower = null)
    {
        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
            return await SearchPackagedJsonAsync(query, includeAdvanced, maxPower);

        var trimmed = query?.Trim() ?? string.Empty;
        using var conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);

        var filterArgs = new List<object>();
        var whereClause = BuildWhereClause(filterArgs, includeAdvanced, maxPower);

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            var sql = $"SELECT e.data_json FROM evocs e{whereClause} ORDER BY e.name LIMIT 100;";
            var rows = conn.Query<DbRow>(sql, filterArgs.ToArray());
            return Deserialize(rows);
        }

        var likeArgs = new List<object> { "%" + trimmed.ToLowerInvariant() + "%" };
        var likeWhere = BuildWhereClause(likeArgs, includeAdvanced, maxPower, " AND ");
        var searchSql = $"SELECT e.data_json FROM evocs e WHERE e.name_lower LIKE ?{likeWhere} ORDER BY e.name LIMIT 20;";
        var searchRows = conn.Query<DbRow>(searchSql, likeArgs.ToArray());
        return Deserialize(searchRows);
    }

    private static async Task<IReadOnlyList<EvocRaw>> LoadFromPackagedJsonAsync()
    {
        var evocations = await DruidEvocationService.GetAllAsync();
        return evocations
            .Select(MapFromPackagedEvocation)
            .ToList();
    }

    private static async Task<IReadOnlyList<EvocRaw>> SearchPackagedJsonAsync(string? query, bool includeAdvanced, int? maxPower)
    {
        var all = await LoadFromPackagedJsonAsync();
        var trimmed = (query ?? string.Empty).Trim();
        IEnumerable<EvocRaw> filtered = all;

        if (!includeAdvanced)
            filtered = filtered.Where(e => !e.isAdvanced);

        if (maxPower.HasValue)
            filtered = filtered.Where(e => e.power <= maxPower.Value);

        if (trimmed.Length > 0)
        {
            filtered = filtered.Where(e =>
                (e.name ?? string.Empty).Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || (e.fields ?? new List<string>()).Any(f => (f ?? string.Empty).Contains(trimmed, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered
            .OrderBy(e => e.power)
            .ThenBy(e => e.name, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();
    }

    private static EvocRaw MapFromPackagedEvocation(DruidEvocationService.EvocRaw source)
    {
        return new EvocRaw
        {
            name = source.name ?? string.Empty,
            power = source.power,
            range = source.range ?? string.Empty,
            duration = source.duration ?? string.Empty,
            verbal = source.verbal ?? string.Empty,
            fields = (source.fields ?? new List<string>())
                .Select(f => (f ?? string.Empty).Trim())
                .Where(f => f.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            description = source.description ?? string.Empty,
            isAdvanced = source.isAdvanced
        };
    }

    private static string BuildWhereClause(List<object> args, bool includeAdvanced, int? maxPower, string prefix = " WHERE ")
    {
        var clauses = new List<string>();
        if (!includeAdvanced)
            clauses.Add("e.is_advanced = 0");
        if (maxPower.HasValue)
        {
            clauses.Add("e.power <= ?");
            args.Add(maxPower.Value);
        }

        if (clauses.Count == 0)
            return string.Empty;

        return $"{prefix}{string.Join(" AND ", clauses)}";
    }

    private static List<EvocRaw> Deserialize(IEnumerable<DbRow> rows)
    {
        var list = new List<EvocRaw>();
        foreach (var row in rows)
        {
            var e = JsonSerializer.Deserialize<EvocRaw>(row.data_json);
            if (e != null) list.Add(e);
        }

        return list;
    }

    private class DbRow { public string data_json { get; set; } = string.Empty; }
}
