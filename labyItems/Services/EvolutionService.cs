using System.Text.Json;
using labyItems.Models.Rules;
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
    private const string LegacyResistanceName = "9th Level Resistance to Spirits and Magic";
    private const string CanonicalResistanceName = "9th Level Resistance to Magic and Spirits";
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
            var list = rows
                .Select(ToEvolutionResult)
                .ToList();

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
        public string AvailabilityDisplay { get; init; } = string.Empty;
        public IReadOnlyList<RuleClause> AvailabilityRules { get; init; } = Array.Empty<RuleClause>();
        public bool CanBuyMultiple { get; init; }
        public IReadOnlyList<string> PreReqs { get; init; } = Array.Empty<string>();
        public int? MaxAvailable { get; init; }
        public bool IsNonStandard { get; init; }
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
                list.Add(ToAbilityResult(r));
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
        var expandedQueries = ExpandAbilityQueryAliases(q).ToList();
        var tokens = expandedQueries
            .SelectMany(term =>
            {
                var normalized = ServiceHelper.NormalizeForNgrams(term.ToLowerInvariant());
                return ServiceHelper.GenerateNGrams(normalized, NGRAM_N);
            })
            .Distinct()
            .ToList();
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
            var list = rows
                .Select(ToEvolutionResult)
                .Take(20)
                .ToList();

            if (table is { } t && t >= 1)
                list = list.Where(l => l.Table == t).ToList();

            if (list.Count == 0)
            {
                var fallback = MergeEvolutionSearchResults(
                    expandedQueries.Select(term => SearchByIndexLike(conn, term, table)));
                return fallback;
            }

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
        var expandedQueries = ExpandAbilityQueryAliases(q).ToList();
        var tokens = expandedQueries
            .SelectMany(term =>
            {
                var normalized = ServiceHelper.NormalizeForNgrams(term.ToLowerInvariant());
                return ServiceHelper.GenerateNGrams(normalized, NGRAM_N);
            })
            .Distinct()
            .ToList();
        if (tokens.Count == 0) return new List<AbilityResult>();

        try
        {
            using var conn = ServiceHelper.OpenReadOnlyConnection();

            if (expandedQueries.All(x => x.Length < NGRAM_N))
            {
                return MergeAbilitySearchResults(expandedQueries.Select(term => SearchAbilitiesByLike(conn, term, table)));
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
            var list = rows
                .Select(ToAbilityResult)
                .Take(20)
                .ToList();

            if (table is { } t && t >= 1)
                list = list.Where(l => l.Table == t).ToList();

            if (list.Count == 0)
                return MergeAbilitySearchResults(expandedQueries.Select(term => SearchAbilitiesByLike(conn, term, table)));

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
            return (list ?? new List<string>())
                .Select(NormalizeAbilityDisplayText)
                .ToList();
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
        var list = rows.Select(ToAbilityResult).ToList();

        if (table is { } t && t >= 1)
            list = list.Where(l => l.Table == t).ToList();

        return list.Take(20).ToList();
    }

    private static IReadOnlyList<EvolutionResult> SearchByIndexLike(SQLite.SQLiteConnection conn, string query, int? table)
    {
        var sql = "SELECT idx, description, cost, table_id FROM evolution WHERE idx LIKE ? ORDER BY table_id, idx LIMIT 50;";
        var rows = conn.Query<EvoRow>(sql, $"%{query}%");
        var list = rows.Select(ToEvolutionResult).ToList();

        if (table is { } t && t >= 1)
            list = list.Where(l => l.Table == t).ToList();

        return list.Take(20).ToList();
    }

    private static AbilityResult ToAbilityResult(AbilityRow row)
    {
        var availability = ParseAvailability(row.available);

        return new AbilityResult
        {
            Index = NormalizeAbilityDisplayText(row.idx),
            Description = NormalizeAbilityDisplayText(row.description),
            Cost = row.cost,
            Table = row.table_id,
            Available = row.available ?? string.Empty,
            AvailabilityDisplay = availability.DisplayText,
            AvailabilityRules = availability.Rules,
            CanBuyMultiple = row.can_buy_multiple != 0,
            PreReqs = ParsePreReqs(row.prereqs_json),
            MaxAvailable = ParseMaxAvailable(row.data_json),
            IsNonStandard = ParseNonStandard(row.data_json)
        };
    }

    private static EvolutionResult ToEvolutionResult(EvoRow row)
    {
        var index = NormalizeAbilityDisplayText(row.idx);
        return new EvolutionResult
        {
            Index = index,
            Description = NormalizeAbilityDisplayText(row.description),
            Cost = row.cost,
            Table = row.table_id,
            IsImmunity = index.IsImmunity()
        };
    }

    private static IReadOnlyList<AbilityResult> MergeAbilitySearchResults(IEnumerable<IReadOnlyList<AbilityResult>> lists)
    {
        var merged = new List<AbilityResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in lists)
        {
            foreach (var item in list)
            {
                var key = $"{item.Table}|{item.Index}";
                if (!seen.Add(key))
                    continue;

                merged.Add(item);
                if (merged.Count >= 20)
                    return merged;
            }
        }

        return merged;
    }

    private static IReadOnlyList<EvolutionResult> MergeEvolutionSearchResults(IEnumerable<IReadOnlyList<EvolutionResult>> lists)
    {
        var merged = new List<EvolutionResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in lists)
        {
            foreach (var item in list)
            {
                var key = $"{item.Table}|{item.Index}";
                if (!seen.Add(key))
                    continue;

                merged.Add(item);
                if (merged.Count >= 20)
                    return merged;
            }
        }

        return merged;
    }

    public static string NormalizeAbilityDisplayText(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        if (string.Equals(text, LegacyResistanceName, StringComparison.OrdinalIgnoreCase))
            return CanonicalResistanceName;

        return text;
    }

    public static IReadOnlyList<string> GetEquivalentAbilityNames(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return Array.Empty<string>();

        if (string.Equals(text, LegacyResistanceName, StringComparison.OrdinalIgnoreCase))
            return new[] { CanonicalResistanceName, LegacyResistanceName };

        if (string.Equals(text, CanonicalResistanceName, StringComparison.OrdinalIgnoreCase))
            return new[] { CanonicalResistanceName, LegacyResistanceName };

        return new[] { text };
    }

    private static IEnumerable<string> ExpandAbilityQueryAliases(string query)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return values;

        values.Add(trimmed);
        foreach (var equivalent in GetEquivalentAbilityNames(trimmed))
            values.Add(equivalent);

        if (trimmed.Contains(LegacyResistanceName, StringComparison.OrdinalIgnoreCase))
            values.Add(trimmed.Replace(LegacyResistanceName, CanonicalResistanceName, StringComparison.OrdinalIgnoreCase));

        if (trimmed.Contains(CanonicalResistanceName, StringComparison.OrdinalIgnoreCase))
            values.Add(trimmed.Replace(CanonicalResistanceName, LegacyResistanceName, StringComparison.OrdinalIgnoreCase));

        return values;
    }

    private static (string DisplayText, IReadOnlyList<RuleClause> Rules) ParseAvailability(string? rawAvailability)
    {
        var text = (rawAvailability ?? string.Empty).Trim();
        if (text.Length == 0)
            return (string.Empty, Array.Empty<RuleClause>());

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String)
                return ((root.GetString() ?? string.Empty).Trim(), Array.Empty<RuleClause>());

            if (root.ValueKind == JsonValueKind.Array)
                return (JoinStringArray(root), Array.Empty<RuleClause>());

            if (root.ValueKind != JsonValueKind.Object)
                return (text, Array.Empty<RuleClause>());

            var rules = ParseAvailabilityRules(root);
            var displayText = ParseAvailabilityDisplay(root);
            if (displayText.Length == 0 && rules.Count > 0)
                displayText = BuildAvailabilityRuleSummary(rules);

            return (displayText, rules);
        }
        catch
        {
            return (text, Array.Empty<RuleClause>());
        }
    }

    private static IReadOnlyList<RuleClause> ParseAvailabilityRules(JsonElement root)
    {
        if (!TryGetProperty(root, "Rules", out var rulesElement)
            || rulesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RuleClause>();
        }

        var clauses = new List<RuleClause>();
        foreach (var ruleElement in rulesElement.EnumerateArray())
        {
            if (ruleElement.ValueKind != JsonValueKind.Object)
                continue;

            var field = ReadStringProperty(ruleElement, "Field");
            if (field.Length == 0)
                continue;

            var opToken = ReadStringProperty(ruleElement, "Operator");
            var comparisonOp = ParseComparisonOp(opToken);
            var values = ParseRuleValues(ruleElement);
            clauses.Add(new RuleClause
            {
                Field = field,
                Operator = comparisonOp,
                Value = values
            });
        }

        return clauses;
    }

    private static string ParseAvailabilityDisplay(JsonElement root)
    {
        if (TryGetProperty(root, "Display", out var displayElement))
            return ParseAvailabilityDisplayValue(displayElement);

        if (TryGetProperty(root, "Label", out var labelElement))
            return ParseAvailabilityDisplayValue(labelElement);

        if (TryGetProperty(root, "Value", out var valueElement))
            return ParseAvailabilityDisplayValue(valueElement);

        return string.Empty;
    }

    private static string ParseAvailabilityDisplayValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => (element.GetString() ?? string.Empty).Trim(),
            JsonValueKind.Array => JoinStringArray(element),
            _ => string.Empty
        };
    }

    private static List<string> ParseRuleValues(JsonElement ruleElement)
    {
        if (!TryGetProperty(ruleElement, "Value", out var valueElement))
            return new List<string>();

        if (valueElement.ValueKind == JsonValueKind.Array)
        {
            return valueElement
                .EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => (v.GetString() ?? string.Empty).Trim())
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (valueElement.ValueKind == JsonValueKind.String)
        {
            var token = (valueElement.GetString() ?? string.Empty).Trim();
            return token.Length == 0 ? new List<string>() : new List<string> { token };
        }

        return new List<string>();
    }

    private static RuleComparisonOp ParseComparisonOp(string token)
    {
        if (token.Equals("NotIn", StringComparison.OrdinalIgnoreCase))
            return RuleComparisonOp.NotIn;

        return RuleComparisonOp.In;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            value = property.Value;
            return true;
        }

        return false;
    }

    private static string ReadStringProperty(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return (value.GetString() ?? string.Empty).Trim();
    }

    private static string JoinStringArray(JsonElement arrayElement)
    {
        if (arrayElement.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var values = arrayElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => (x.GetString() ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.Count == 0 ? string.Empty : string.Join(", ", values);
    }

    private static string BuildAvailabilityRuleSummary(IReadOnlyList<RuleClause> rules)
    {
        if (rules.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        foreach (var rule in rules)
        {
            var values = rule.Value
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .ToList();
            var valuesText = values.Count == 0 ? "any" : string.Join("/", values);
            var opText = rule.Operator == RuleComparisonOp.NotIn ? "not in" : "in";
            parts.Add($"{rule.Field} {opText} {valuesText}");
        }

        return string.Join("; ", parts);
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

    private static bool ParseNonStandard(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("nonStandard", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("non-standard", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind == JsonValueKind.True)
                        return true;

                    if (property.Value.ValueKind == JsonValueKind.Number
                        && property.Value.TryGetInt32(out var numeric)
                        && numeric != 0)
                    {
                        return true;
                    }

                    if (property.Value.ValueKind == JsonValueKind.String
                        && bool.TryParse(property.Value.GetString(), out var parsedBool))
                    {
                        return parsedBool;
                    }
                }
            }
        }
        catch
        {
            // malformed data_json; treat as standard
        }

        return false;
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
