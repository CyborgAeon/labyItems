using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using SQLite;
using labyItems.Pages;
using labyItems.Pages.Calculator;
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
    private static IReadOnlyList<AbilityResult>? _abilityCache;

    private static string? _dbPath;
    private const int NGRAM_N = 3;

    private static string? EnsureDbPath()
    {
        if (!string.IsNullOrWhiteSpace(_dbPath) && File.Exists(_dbPath))
            return _dbPath;

        var appDb = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
        if (File.Exists(appDb))
        {
            _dbPath = appDb;
            return _dbPath;
        }

        // Development fallback path (CI artifact)
        var devCandidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "output", "laby.db")
        };

        foreach (var candidate in devCandidates)
        {
            if (File.Exists(candidate))
            {
                _dbPath = candidate;
                return _dbPath;
            }
        }

        _dbPath = null;
        return null;
    }

    public static async Task<IReadOnlyList<General.Result>> GetAllAsync()
    {
        if (_cache is not null) return _cache;

        var dbPath = EnsureDbPath();
        if (string.IsNullOrEmpty(dbPath))
        {
            var ex = new InvalidOperationException("Evolution DB not found; ensure laby.db is present in app data or available during development.");
            LogDbError("GetAll evolution", ex);
            throw ex;
        }

        try
        {
            using var conn = new SQLite.SQLiteConnection(dbPath, SQLite.SQLiteOpenFlags.ReadOnly);

            var rows = conn.Query<EvoRow>("SELECT idx, description, cost, table_id FROM evolution ORDER BY table_id, idx;");
            var list = new List<General.Result>();
            foreach (var r in rows)
            {
                list.Add(new General.Result { Index = r.idx, Description = r.description ?? string.Empty, Cost = r.cost, Table = r.table_id, IsImmunity = r.idx.IsImmunity() });
            }

            _cache = list;
            return _cache;
        }
        catch (Exception ex)
        {
            LogDbError("GetAll evolution", ex);
            throw;
        }
    }

    public sealed record AbilityResult
    {
        public string Index { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int Cost { get; init; }
        public int Table { get; init; }
        public string Available { get; init; } = string.Empty;
        public bool CanBuyMultiple { get; init; }
        public IReadOnlyList<string> PreReqs { get; init; } = Array.Empty<string>();
    }

    public static async Task<IReadOnlyList<AbilityResult>> GetAllAbilitiesAsync()
    {
        if (_abilityCache is not null) return _abilityCache;

        var dbPath = EnsureDbPath();
        if (string.IsNullOrEmpty(dbPath))
        {
            var ex = new InvalidOperationException("Abilities DB not found; ensure laby.db is present in app data or available during development.");
            LogDbError("GetAll abilities", ex);
            throw ex;
        }

        try
        {
            using var conn = new SQLite.SQLiteConnection(dbPath, SQLite.SQLiteOpenFlags.ReadOnly);

            var rows = conn.Query<AbilityRow>("SELECT idx, description, cost, available, table_id, can_buy_multiple, prereqs_json FROM evolution ORDER BY table_id, idx;");
            if (rows.Count == 0)
            {
                var emptyEx = new InvalidOperationException("Evolution table returned zero rows. Ensure the abilities data has been migrated into laby.db.");
                LogDbError("GetAll abilities", emptyEx);
                return new List<AbilityResult>();
            }

            var list = new List<AbilityResult>();
            foreach (var r in rows)
            {
                var preReqs = ParsePreReqs(r.prereqs_json);
                list.Add(new AbilityResult
                {
                    Index = r.idx,
                    Description = r.description ?? string.Empty,
                    Cost = r.cost,
                    Table = r.table_id,
                    Available = r.available ?? string.Empty,
                    CanBuyMultiple = r.can_buy_multiple != 0,
                    PreReqs = preReqs
                });
            }

            _abilityCache = list;
            return _abilityCache;
        }
        catch (Exception ex)
        {
            LogDbError("GetAll abilities", ex);
            throw;
        }
    }

    public static async Task<IReadOnlyList<General.Result>> SearchByIndexAsync(string? query, int? table = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync();

        var dbPath = EnsureDbPath();
        if (string.IsNullOrEmpty(dbPath))
        {
            var ex = new InvalidOperationException("Evolution DB not found; ensure laby.db is present in app data or available during development.");
            LogDbError("Search evolution", ex);
            throw ex;
        }

        var q = query.Trim();
        var normalized = NormalizeForNgrams(q.ToLowerInvariant());
        var tokens = GenerateNGrams(normalized, NGRAM_N).Distinct().ToList();
        if (tokens.Count == 0) return new List<General.Result>();

        try
        {
            using var conn = new SQLite.SQLiteConnection(dbPath, SQLite.SQLiteOpenFlags.ReadOnly);

        var paramNames = new List<string>();
        var args = new List<object>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var p = "@p" + i;
            paramNames.Add(p);
            args.Add(tokens[i]);
        }

        var inClause = string.Join(",", paramNames);
        var sql = $"SELECT e.idx, e.description, e.cost, e.table_id FROM evolution e JOIN (SELECT evolution_id, COUNT(*) as ct FROM evolution_ngrams WHERE token IN ({inClause}) GROUP BY evolution_id ORDER BY ct DESC LIMIT 50) g ON e.id = g.evolution_id;";

            var rows = conn.Query<EvoRow>(sql, args.ToArray());
            var list = rows.Select(r => new General.Result { Index = r.idx, Description = r.description ?? string.Empty, Cost = r.cost, Table = r.table_id, IsImmunity = r.idx.IsImmunity() }).Take(20).ToList();

            if (table is { } t && t >= 1)
                list = list.Where(l => l.Table == t).ToList();

            return list;
        }
        catch (Exception ex)
        {
            LogDbError("Search evolution", ex);
            throw;
        }
    }

    public static async Task<IReadOnlyList<AbilityResult>> SearchAbilitiesAsync(string? query, int? table = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAbilitiesAsync();

        var dbPath = EnsureDbPath();
        if (string.IsNullOrEmpty(dbPath))
        {
            var ex = new InvalidOperationException("Evolution DB not found; ensure laby.db is present in app data or available during development.");
            LogDbError("Search abilities", ex);
            throw ex;
        }

        var q = query.Trim();
        var normalized = NormalizeForNgrams(q.ToLowerInvariant());
        var tokens = GenerateNGrams(normalized, NGRAM_N).Distinct().ToList();
        if (tokens.Count == 0) return new List<AbilityResult>();

        try
        {
            using var conn = new SQLite.SQLiteConnection(dbPath, SQLite.SQLiteOpenFlags.ReadOnly);

            if (q.Length < NGRAM_N)
            {
                return SearchAbilitiesByLike(conn, q, table);
            }

            var paramNames = new List<string>();
            var args = new List<object>();
            for (int i = 0; i < tokens.Count; i++)
            {
                var p = "@p" + i;
                paramNames.Add(p);
                args.Add(tokens[i]);
            }

            var inClause = string.Join(",", paramNames);
            var sql = $"SELECT e.idx, e.description, e.cost, e.available, e.table_id, e.can_buy_multiple, e.prereqs_json FROM evolution e JOIN (SELECT evolution_id, COUNT(*) as ct FROM evolution_ngrams WHERE token IN ({inClause}) GROUP BY evolution_id ORDER BY ct DESC LIMIT 50) g ON e.id = g.evolution_id;";

            var rows = conn.Query<AbilityRow>(sql, args.ToArray());
            var list = rows.Select(r => new AbilityResult
            {
                Index = r.idx,
                Description = r.description ?? string.Empty,
                Cost = r.cost,
                Table = r.table_id,
                Available = r.available ?? string.Empty,
                CanBuyMultiple = r.can_buy_multiple != 0,
                PreReqs = ParsePreReqs(r.prereqs_json)
            })
                .Take(20)
                .ToList();

            if (table is { } t && t >= 1)
                list = list.Where(l => l.Table == t).ToList();

            if (list.Count == 0)
                return SearchAbilitiesByLike(conn, q, table);

            return list;
        }
        catch (Exception ex)
        {
            LogDbError("Search abilities", ex);
            throw;
        }
    }

    public static void InvalidateCache()
    {
        _cache = null;
        _abilityCache = null;
    }

    private static int TryParseCost(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return 0;

        var digitsOnly = System.Text.RegularExpressions.Regex.Replace(input, "[^0-9]", "");
        return int.TryParse(digitsOnly, out var value) ? value : 0;
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

    private static IReadOnlyList<string> ParsePreReqs(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static IReadOnlyList<AbilityResult> SearchAbilitiesByLike(SQLite.SQLiteConnection conn, string query, int? table)
    {
        var sql = "SELECT idx, description, cost, available, table_id, can_buy_multiple, prereqs_json FROM evolution WHERE idx LIKE ? ORDER BY table_id, idx LIMIT 50;";
        var rows = conn.Query<AbilityRow>(sql, $"%{query}%");
        var list = rows.Select(r => new AbilityResult
        {
            Index = r.idx,
            Description = r.description ?? string.Empty,
            Cost = r.cost,
            Table = r.table_id,
            Available = r.available ?? string.Empty,
            CanBuyMultiple = r.can_buy_multiple != 0,
            PreReqs = ParsePreReqs(r.prereqs_json)
        }).ToList();

        if (table is { } t && t >= 1)
            list = list.Where(l => l.Table == t).ToList();

        return list.Take(20).ToList();
    }

    private static void LogDbError(string context, Exception ex)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[DB] {context}: {ex}");
            Console.WriteLine($"[DB] {context}: {ex}");
        }
        catch
        {
            // ignore logging failures
        }
    }

    private class EvoRow
    {
        public string idx { get; set; } = string.Empty;
        public string? description { get; set; }
        public int cost { get; set; }
        public int table_id { get; set; }
    }
    private class AbilityRow
    {
        public string idx { get; set; } = string.Empty;
        public string? description { get; set; }
        public int cost { get; set; }
        public string? available { get; set; }
        public int table_id { get; set; }
        public int can_buy_multiple { get; set; }
        public string? prereqs_json { get; set; }
    }
}
