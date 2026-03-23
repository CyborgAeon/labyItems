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
        await EnsureDatabaseInitializedAsync();
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
        public string AbilityRef { get; init; } = string.Empty;
        public string Available { get; init; } = string.Empty;
        public string SourceBook { get; init; } = string.Empty;
        public string AvailabilityDisplay { get; init; } = string.Empty;
        public IReadOnlyList<RuleClause> AvailabilityRules { get; init; } = Array.Empty<RuleClause>();
        public bool CanBuyMultiple { get; init; }
        public IReadOnlyList<string> PreReqs { get; init; } = Array.Empty<string>();
        public int? MaxAvailable { get; init; }
        public int MaxAcIncrease { get; init; }
        public IReadOnlyList<string> ChoiceSetRefs { get; init; } = Array.Empty<string>();
        public bool IsNonStandard { get; init; }
    }

    public static async Task<IReadOnlyList<AbilityResult>> GetAllAbilitiesAsync()
    {
        await EnsureDatabaseInitializedAsync();
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
        await EnsureDatabaseInitializedAsync();
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
        await EnsureDatabaseInitializedAsync();
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

    private static async Task EnsureDatabaseInitializedAsync()
    {
        try
        {
            var services = ResolveMauiServiceProvider();
            if (services == null)
                return;

            var initializerType = ResolveDatabaseInitializerType();
            if (initializerType == null)
                return;

            var initializer = services.GetService(initializerType);
            if (initializer == null)
                return;

            var initMethod = initializerType.GetMethod(
                "InitializeAsync",
                new[] { typeof(CancellationToken) });

            if (initMethod?.Invoke(initializer, new object[] { CancellationToken.None }) is Task initTask)
                await initTask.ConfigureAwait(false);
        }
        catch
        {
            // If startup initialization fails we still allow legacy DB fallback reads.
        }
    }

    private static Type? ResolveDatabaseInitializerType()
    {
        const string typeName = "labyItems.Services.IDatabaseInitializer";

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var resolved = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
            if (resolved != null)
                return resolved;
        }

        return null;
    }

    private static IServiceProvider? ResolveMauiServiceProvider()
    {
        var appType = ResolveType("Microsoft.Maui.Controls.Application");
        if (appType == null)
            return null;

        var currentApp = appType.GetProperty("Current")?.GetValue(null);
        if (currentApp == null)
            return null;

        var handler = currentApp.GetType().GetProperty("Handler")?.GetValue(currentApp);
        if (handler == null)
            return null;

        var mauiContext = handler.GetType().GetProperty("MauiContext")?.GetValue(handler);
        if (mauiContext == null)
            return null;

        return mauiContext.GetType().GetProperty("Services")?.GetValue(mauiContext) as IServiceProvider;
    }

    private static Type? ResolveType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var resolved = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (resolved != null)
                return resolved;
        }

        return null;
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
            AbilityRef = ParseAbilityRef(row.data_json),
            Available = row.available ?? string.Empty,
            SourceBook = ParseSourceBook(row.data_json),
            AvailabilityDisplay = availability.DisplayText,
            AvailabilityRules = availability.Rules,
            CanBuyMultiple = row.can_buy_multiple != 0,
            PreReqs = ParsePreReqs(row.prereqs_json),
            MaxAvailable = ParseMaxAvailable(row.data_json),
            MaxAcIncrease = ParseMaxAcIncrease(row.data_json),
            ChoiceSetRefs = ParseChoiceSetRefs(row.data_json),
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
            {
                var arrayRules = ParseAvailabilityRules(root);
                if (arrayRules.Count > 0)
                {
                    var summary = BuildAvailabilityRuleSummary(arrayRules);
                    return (summary, arrayRules);
                }

                return (JoinStringArray(root), Array.Empty<RuleClause>());
            }

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
        if (root.ValueKind == JsonValueKind.Array)
            return ParseAvailabilityRuleArray(root);

        if (!TryGetProperty(root, "Rules", out var rulesElement)
            || rulesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RuleClause>();
        }

        return ParseAvailabilityRuleArray(rulesElement);
    }

    private static IReadOnlyList<RuleClause> ParseAvailabilityRuleArray(JsonElement rulesElement)
    {
        if (rulesElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<RuleClause>();

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
            var specialisationKey = ReadStringProperty(ruleElement, "SpecialisationKey");
            if (specialisationKey.Length == 0)
                specialisationKey = ReadStringProperty(ruleElement, "SpecializationKey");
            clauses.Add(new RuleClause
            {
                Field = field,
                Operator = comparisonOp,
                Value = values,
                SpecialisationKey = specialisationKey.Length == 0 ? null : specialisationKey
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
        if (token.Equals("Only", StringComparison.OrdinalIgnoreCase))
            return RuleComparisonOp.Only;

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
            var opText = rule.Operator switch
            {
                RuleComparisonOp.NotIn => "not in",
                RuleComparisonOp.Only => "only",
                _ => "in"
            };
            var keyHint = string.IsNullOrWhiteSpace(rule.SpecialisationKey)
                ? string.Empty
                : $" [{rule.SpecialisationKey}]";
            parts.Add($"{rule.Field}{keyHint} {opText} {valuesText}");
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

    private static string ParseSourceBook(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return string.Empty;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("sourceBook", StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Equals("source_book", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.String)
                    return (property.Value.GetString() ?? string.Empty).Trim();

                return string.Empty;
            }
        }
        catch
        {
            // malformed data_json; treat source book as unknown
        }

        return string.Empty;
    }

    private static string ParseAbilityRef(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return string.Empty;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("abilityRef", StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Equals("ability_ref", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.String)
                    return (property.Value.GetString() ?? string.Empty).Trim();

                return string.Empty;
            }
        }
        catch
        {
            // malformed data_json
        }

        return string.Empty;
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

    private static int ParseMaxAcIncrease(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return 0;

            if (TryParseMaxAcIncreaseFromElement(doc.RootElement, out var directAmount))
                return directAmount;

            if (TryGetProperty(doc.RootElement, "data", out var dataElement)
                && dataElement.ValueKind == JsonValueKind.Object
                && TryParseMaxAcIncreaseFromElement(dataElement, out var nestedAmount))
            {
                return nestedAmount;
            }
        }
        catch
        {
            // malformed data_json; treat as no max AC increment
        }

        return 0;
    }

    private static bool TryParseMaxAcIncreaseFromElement(JsonElement element, out int amount)
    {
        amount = 0;
        var effectType = ReadStringProperty(element, "effectType");
        if (effectType.Length == 0)
            effectType = ReadStringProperty(element, "effect_type");

        if (!effectType.Equals("increase:maxAC", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryGetProperty(element, "amount", out var amountElement))
            return false;

        var parsedAmount = ParsePositiveInt(amountElement);
        if (parsedAmount <= 0)
            return false;

        amount = parsedAmount;
        return true;
    }

    private static int ParsePositiveInt(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var asNumber))
            return Math.Max(0, asNumber);

        if (value.ValueKind == JsonValueKind.String)
        {
            var token = (value.GetString() ?? string.Empty).Trim();
            if (int.TryParse(token, out var asText))
                return Math.Max(0, asText);
        }

        return 0;
    }

    private static IReadOnlyList<string> ParseChoiceSetRefs(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return Array.Empty<string>();

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return Array.Empty<string>();

            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var propertyName = property.Name;
                var isSingle = propertyName.Equals("choiceSetRef", StringComparison.OrdinalIgnoreCase)
                               || propertyName.Equals("choice_set_ref", StringComparison.OrdinalIgnoreCase);
                var isMany = propertyName.Equals("choiceSetRefs", StringComparison.OrdinalIgnoreCase)
                             || propertyName.Equals("choice_set_refs", StringComparison.OrdinalIgnoreCase);
                if (!isSingle && !isMany)
                    continue;

                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var single = (property.Value.GetString() ?? string.Empty).Trim();
                    if (single.Length > 0)
                        values.Add(single);
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                        continue;

                    var choiceSetRef = (item.GetString() ?? string.Empty).Trim();
                    if (choiceSetRef.Length > 0)
                        values.Add(choiceSetRef);
                }
            }

            return values.Count == 0
                ? Array.Empty<string>()
                : values.ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
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
