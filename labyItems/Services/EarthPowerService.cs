using System.Text.Json;
using System.Text;
using SQLite;

namespace labyItems.Services;

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
    private const int NGRAM_N = 3;

    public static bool HasDatabase => !string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath);

    static EarthPowerService()
    {
        var appDb = Path.Combine(FileSystem.AppDataDirectory, "default.db");
        if (File.Exists(appDb))
        {
            _dbPath = appDb;
            return;
        }

        var devCandidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "output", "evocs.db"),
            Path.Combine(Directory.GetCurrentDirectory(), "output", "default.db")
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

    public static Task<IReadOnlyList<EvocRaw>> GetAllAsync()
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

            return Task.FromResult<IReadOnlyList<EvocRaw>>(list);
        }

        throw new InvalidOperationException("Evocations DB not found; please install default.db in app data or provide the DB during development.");
    }

    public static Task<IReadOnlyList<EvocRaw>> SearchAsync(string query, bool includeAdvanced = true, int? maxPower = null)
    {
        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
            throw new InvalidOperationException("Evocations DB not found; ensure default.db is installed and accessible.");

        var trimmed = query?.Trim() ?? string.Empty;
        using var conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);

        var filterArgs = new List<object>();
        var whereClause = BuildWhereClause(filterArgs, includeAdvanced, maxPower);

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            var sql = $"SELECT e.data_json FROM evocs e{whereClause} ORDER BY e.name LIMIT 100;";
            var rows = conn.Query<DbRow>(sql, filterArgs.ToArray());
            return Task.FromResult<IReadOnlyList<EvocRaw>>(Deserialize(rows));
        }

        var normalized = NormalizeForNgrams(trimmed.ToLowerInvariant());
        var tokens = GenerateNGrams(normalized, NGRAM_N).Distinct().ToList();
        if (tokens.Count == 0)
            return Task.FromResult<IReadOnlyList<EvocRaw>>(Array.Empty<EvocRaw>());

        var tokenPlaceholders = string.Join(",", Enumerable.Repeat("?", tokens.Count));
        var args = tokens.Cast<object>().ToList();
        whereClause = BuildWhereClause(args, includeAdvanced, maxPower);

        var sqlWithTokens = $"SELECT e.data_json FROM evocs e JOIN (SELECT evoc_id, COUNT(*) as ct FROM evoc_ngrams WHERE token IN ({tokenPlaceholders}) GROUP BY evoc_id ORDER BY ct DESC LIMIT 50) g ON e.id = g.evoc_id{whereClause} ORDER BY ct DESC, e.name LIMIT 50;";
        var tokenRows = conn.Query<DbRow>(sqlWithTokens, args.ToArray());
        var tokenResults = Deserialize(tokenRows);
        if (tokenResults.Count > 0)
            return Task.FromResult<IReadOnlyList<EvocRaw>>(tokenResults.Take(20).ToList());

        var likeArgs = new List<object> { "%" + trimmed.ToLowerInvariant() + "%" };
        var likeWhere = BuildWhereClause(likeArgs, includeAdvanced, maxPower, " AND ");
        var fallbackSql = $"SELECT e.data_json FROM evocs e WHERE e.name_lower LIKE ?{likeWhere} ORDER BY e.name LIMIT 20;";
        var fallbackRows = conn.Query<DbRow>(fallbackSql, likeArgs.ToArray());
        return Task.FromResult<IReadOnlyList<EvocRaw>>(Deserialize(fallbackRows));
    }

    private static string NormalizeForNgrams(string s)
    {
        var b = new StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)) b.Append(ch);
            else b.Append(' ');
        }
        var normalized = string.Join(' ', b.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        return normalized;
    }

    private static IEnumerable<string> GenerateNGrams(string s, int n)
    {
        var t = s.Replace(" ", " ");
        if (t.Length <= n)
        {
            yield return t;
            yield break;
        }

        for (int i = 0; i <= t.Length - n; i++)
            yield return t.Substring(i, n);
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
