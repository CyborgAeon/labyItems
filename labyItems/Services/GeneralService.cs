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

    private static string? _dbPath;
    private const int NGRAM_N = 3;

    static GeneralService()
    {
        var appDb = Path.Combine(FileSystem.AppDataDirectory, "default.db");
        if (File.Exists(appDb))
        {
            _dbPath = appDb;
            return;
        }

        // Development fallback paths (support both 'evocs.db' and CI artifact 'default.db')
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

    public static async Task<IReadOnlyList<General.Result>> GetAllAsync()
    {
        if (_cache is not null) return _cache;

        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
            throw new InvalidOperationException("Evolution DB not found; ensure default.db is present in app data or available during development.");

        using var conn = new SQLite.SQLiteConnection(_dbPath, SQLite.SQLiteOpenFlags.ReadOnly);

        var rows = conn.Query<EvoRow>("SELECT idx, description, cost, table_id FROM evolution ORDER BY table_id, idx;");
        var list = new List<General.Result>();
        foreach (var r in rows)
        {
            list.Add(new General.Result { Index = r.idx, Description = r.description ?? string.Empty, Cost = r.cost, Table = r.table_id, IsImmunity = r.idx.IsImmunity() });
        }

        _cache = list;
        return _cache;
    }

    public static async Task<IReadOnlyList<General.Result>> SearchByIndexAsync(string? query, int? table = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync();

        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
            throw new InvalidOperationException("Evolution DB not found; ensure default.db is present in app data or available during development.");

        var q = query.Trim();
        var normalized = NormalizeForNgrams(q.ToLowerInvariant());
        var tokens = GenerateNGrams(normalized, NGRAM_N).Distinct().ToList();
        if (tokens.Count == 0) return new List<General.Result>();

        using var conn = new SQLite.SQLiteConnection(_dbPath, SQLite.SQLiteOpenFlags.ReadOnly);

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

    public static void InvalidateCache() => _cache = null;

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

    private class EvoRow { public string idx { get; set; } = string.Empty; public string? description { get; set; } public int cost { get; set; } public int table_id { get; set; } }
}
