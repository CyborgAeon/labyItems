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

    // DB-backed cache will be used if default DB is present; otherwise JSON fallback
    private static List<EvocRaw>? _cache;
    private static string? _dbPath;
    private const int NGRAM_N = 3;

    static EarthPowerService()
    {
        var appDb = Path.Combine(FileSystem.AppDataDirectory, "default.db");
        if (File.Exists(appDb))
        {
            _dbPath = appDb;
            Console.WriteLine($"[EarthPowerService] Using app DB: {_dbPath}");
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
                Console.WriteLine($"[EarthPowerService] Using dev DB: {_dbPath}");
                return;
            }
        }

        _dbPath = null;
        Console.WriteLine("[EarthPowerService] No DB found; set _dbPath = null");
    }

    public static async Task<IReadOnlyList<EvocRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        if (!string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath))
        {
            Console.WriteLine($"[EarthPowerService] Opening DB at {_dbPath}");
            using var conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);
            try
            {
                var total = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM evocs;");
                var ngrams = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM evoc_ngrams;");
                Console.WriteLine($"[EarthPowerService] evocs rows={total} evoc_ngrams={ngrams}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EarthPowerService] Warning: failed to read counts: {ex.Message}");
            }

            var rows = conn.Query<DbRow>("SELECT data_json FROM evocs ORDER BY name;");
            var list = new List<EvocRaw>();
            foreach (var row in rows)
            {
                try
                {
                    var e = JsonSerializer.Deserialize<EvocRaw>(row.data_json);
                    if (e != null) list.Add(e);
                }
                catch { /* ignore malformed rows */ }
            }

            _cache = list;
            return _cache;
        }

        throw new InvalidOperationException("Evocations DB not found; please install default.db in app data or provide the DB during development.");
    }

    public static async Task<IReadOnlyList<EvocRaw>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync();

        query = query.Trim();

        // If DB available, use n-gram token matching for substring search
        if (!string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath))
        {
            try
            {
                var normalized = NormalizeForNgrams(query.ToLowerInvariant());
                var tokens = GenerateNGrams(normalized, NGRAM_N).Distinct().ToList();
                Console.WriteLine($"[EarthPowerService] Search query='{query}' normalized='{normalized}' tokens=[{string.Join(',', tokens)}]");
                if (tokens.Count == 0)
                    return Array.Empty<EvocRaw>();

                using var conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);

                // Build paramized IN list
                var paramNames = new List<string>();
                var args = new List<object>();
                for (int i = 0; i < tokens.Count; i++)
                {
                    var p = "@p" + i;
                    paramNames.Add(p);
                    args.Add(tokens[i]);
                }

                var inClause = string.Join(",", paramNames);
                var sql = $"SELECT e.data_json FROM evocs e JOIN (SELECT evoc_id, COUNT(*) as ct FROM evoc_ngrams WHERE token IN ({inClause}) GROUP BY evoc_id ORDER BY ct DESC LIMIT 50) g ON e.id = g.evoc_id;";
                Console.WriteLine($"[EarthPowerService] SQL={sql} params=[{string.Join(',', args)}]");

                var rows = conn.Query<DbRow>(sql, args.ToArray());
                Console.WriteLine($"[EarthPowerService] Rows returned: {rows.Count}");
                var list = new List<EvocRaw>();
                foreach (var row in rows)
                {
                    try
                    {
                        var e = JsonSerializer.Deserialize<EvocRaw>(row.data_json);
                        if (e != null) list.Add(e);
                    }
                    catch { }
                }

                if (list.Count > 0) return list.Take(20).ToList();

                // Fallback: try name contains search
                var like = "%" + query.ToLowerInvariant() + "%";
                var nameRows = conn.Query<DbRow>("SELECT data_json FROM evocs WHERE name_lower LIKE ? ORDER BY name LIMIT 20;", like);
                list.Clear();
                foreach (var row in nameRows)
                {
                    try { var e = JsonSerializer.Deserialize<EvocRaw>(row.data_json); if (e != null) list.Add(e); } catch { }
                }
                return list;
            }
            catch
            {
                throw new InvalidOperationException("Evocations DB not found or query failed; ensure default.db is installed.");
            }
        }

        throw new InvalidOperationException("Evocations DB not found; ensure default.db is installed and accessible.");
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

    private class DbRow { public string data_json { get; set; } = string.Empty; }
}
