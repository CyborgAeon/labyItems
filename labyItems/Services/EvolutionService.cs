using System.Text.Json;
using SQLite;

namespace labyItems.Services;

public static class AbilityHelper
{
    public static bool IsImmunity(this string fromIndex)
    {
        if (fromIndex.Contains("Immunity!")) return true;
        else return false;
    }
}

public static class EvolutionService
{
    private static IReadOnlyList<EvolutionResult>? _cache;
    private static IReadOnlyList<AbilityResult>? _abilityCache;
    private const int NGRAM_N = 3;

    public sealed record EvolutionResult
    {
        public string Index { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int Cost { get; init; }
        public int Table { get; init; }
        public bool? IsImmunity { get; init; }
    }

    public static async Task<IReadOnlyList<EvolutionResult>> GetAllAsync()
    {
        if (_cache is not null) return _cache;

        try
        {
            using var conn = ServiceHelper.OpenReadOnlyConnection();
            var rows = conn.Query<EvoRow>("SELECT idx, description, cost, table_id FROM evolution ORDER BY table_id, idx;");
            var list = rows.Select(r => new EvolutionResult
            {
                Index = r.idx,
                Description = r.description ?? string.Empty,
                Cost = r.cost,
                Table = r.table_id,
                IsImmunity = r.idx.IsImmunity()
            }).ToList();

            _cache = list;
            return _cache;
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("GetAll evolution", ex);
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
        public int? MaxAvailable { get; init; }
    }

    public static async Task<IReadOnlyList<AbilityResult>> GetAllAbilitiesAsync()
    {
        if (_abilityCache is not null) return _abilityCache;

        try
        {
            using var conn = ServiceHelper.OpenReadOnlyConnection();

            var rows = conn.Query<AbilityRow>("SELECT idx, description, cost, available, table_id, can_buy_multiple, prereqs_json, data_json FROM evolution ORDER BY table_id, idx;");
            if (rows.Count == 0)
            {
                var emptyEx = new InvalidOperationException("Evolution table returned zero rows. Ensure the abilities data has been migrated into laby.db.");
                ServiceHelper.LogDbError("GetAll abilities", emptyEx);
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
                    PreReqs = preReqs,
                    MaxAvailable = ParseMaxAvailable(r.data_json)
                });
            }

            _abilityCache = list;
            return _abilityCache;
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("GetAll abilities", ex);
            throw;
        }
    }

    public static async Task<IReadOnlyList<EvolutionResult>> SearchByIndexAsync(string? query, int? table = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync();

        var q = query.Trim();
        var normalized = ServiceHelper.NormalizeForNgrams(q.ToLowerInvariant());
        var tokens = ServiceHelper.GenerateNGrams(normalized, NGRAM_N).Distinct().ToList();
        if (tokens.Count == 0) return new List<EvolutionResult>();

        try
        {
            using var conn = ServiceHelper.OpenReadOnlyConnection();

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
            var list = rows.Select(r => new EvolutionResult
            {
                Index = r.idx,
                Description = r.description ?? string.Empty,
                Cost = r.cost,
                Table = r.table_id,
                IsImmunity = r.idx.IsImmunity()
            }).Take(20).ToList();

            if (table is { } t && t >= 1)
                list = list.Where(l => l.Table == t).ToList();

            return list;
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Search evolution", ex);
            throw;
        }
    }

    public static async Task<IReadOnlyList<AbilityResult>> SearchAbilitiesAsync(string? query, int? table = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAbilitiesAsync();

        var q = query.Trim();
        var normalized = ServiceHelper.NormalizeForNgrams(q.ToLowerInvariant());
        var tokens = ServiceHelper.GenerateNGrams(normalized, NGRAM_N).Distinct().ToList();
        if (tokens.Count == 0) return new List<AbilityResult>();

        try
        {
            using var conn = ServiceHelper.OpenReadOnlyConnection();

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
            var sql = $"SELECT e.idx, e.description, e.cost, e.available, e.table_id, e.can_buy_multiple, e.prereqs_json, e.data_json FROM evolution e JOIN (SELECT evolution_id, COUNT(*) as ct FROM evolution_ngrams WHERE token IN ({inClause}) GROUP BY evolution_id ORDER BY ct DESC LIMIT 50) g ON e.id = g.evolution_id;";

            var rows = conn.Query<AbilityRow>(sql, args.ToArray());
            var list = rows.Select(r => new AbilityResult
            {
                Index = r.idx,
                Description = r.description ?? string.Empty,
                Cost = r.cost,
                Table = r.table_id,
                Available = r.available ?? string.Empty,
                CanBuyMultiple = r.can_buy_multiple != 0,
                PreReqs = ParsePreReqs(r.prereqs_json),
                MaxAvailable = ParseMaxAvailable(r.data_json)
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
            ServiceHelper.LogDbError("Search abilities", ex);
            throw;
        }
    }

    public static void InvalidateCache()
    {
        _cache = null;
        _abilityCache = null;
        AbilityDetailsLookupService.InvalidateCache();
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
        var sql = "SELECT idx, description, cost, available, table_id, can_buy_multiple, prereqs_json, data_json FROM evolution WHERE idx LIKE ? ORDER BY table_id, idx LIMIT 50;";
        var rows = conn.Query<AbilityRow>(sql, $"%{query}%");
        var list = rows.Select(r => new AbilityResult
        {
            Index = r.idx,
            Description = r.description ?? string.Empty,
            Cost = r.cost,
            Table = r.table_id,
            Available = r.available ?? string.Empty,
            CanBuyMultiple = r.can_buy_multiple != 0,
            PreReqs = ParsePreReqs(r.prereqs_json),
            MaxAvailable = ParseMaxAvailable(r.data_json)
        }).ToList();

        if (table is { } t && t >= 1)
            list = list.Where(l => l.Table == t).ToList();

        return list.Take(20).ToList();
    }

    private static int? ParseMaxAvailable(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("maxAvailable", StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Equals("max_available", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return TryParseMaxAvailableValue(property.Value);
            }
        }
        catch
        {
            // malformed data_json; treat as unbounded
        }

        return null;
    }

    private static int? TryParseMaxAvailableValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var asNumber) && asNumber > 0)
            return asNumber;

        if (value.ValueKind == JsonValueKind.String)
        {
            var token = (value.GetString() ?? string.Empty).Trim();
            if (int.TryParse(token, out var asText) && asText > 0)
                return asText;
        }

        return null;
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
        public string? data_json { get; set; }
    }
}
