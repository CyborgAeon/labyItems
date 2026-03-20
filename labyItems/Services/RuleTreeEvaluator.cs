using labyItems.Models.Rules;

namespace labyItems.Services;

public static class RuleTreeEvaluator
{
    public static bool Evaluate(
        IEnumerable<RuleClause>? rules,
        Func<string, IEnumerable<string>> resolveFieldValues,
        Func<string, string>? normalizeValue = null)
        => Evaluate(
            rules,
            (rule, field) => resolveFieldValues(field),
            normalizeValue);

    public static bool Evaluate(
        IEnumerable<RuleClause>? rules,
        Func<RuleClause, string, IEnumerable<string>> resolveFieldValues,
        Func<string, string>? normalizeValue = null)
    {
        var list = (rules ?? Array.Empty<RuleClause>())
            .Where(r => r != null && r.IsValid)
            .ToList();

        if (list.Count == 0)
            return true;

        normalizeValue ??= DefaultNormalizeValue;
        return list.All(rule => EvaluateRule(rule, resolveFieldValues, normalizeValue));
    }

    public static bool ContainsPositiveInValue(
        IEnumerable<RuleClause>? rules,
        string field,
        string value,
        Func<string, string>? normalizeField = null,
        Func<string, string>? normalizeValue = null)
    {
        normalizeField ??= DefaultNormalizeField;
        normalizeValue ??= DefaultNormalizeValue;

        var wantedField = normalizeField(field);
        var wantedValue = normalizeValue(value);
        if (wantedField.Length == 0 || wantedValue.Length == 0)
            return false;

        foreach (var rule in rules ?? Array.Empty<RuleClause>())
        {
            if (rule == null || !rule.IsValid || (rule.Operator != RuleComparisonOp.In && rule.Operator != RuleComparisonOp.Only))
                continue;

            var fieldTokens = (rule.Field ?? string.Empty)
                .Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(normalizeField)
                .Where(x => x.Length > 0)
                .ToList();

            if (!fieldTokens.Contains(wantedField, StringComparer.Ordinal))
                continue;

            var values = NormalizeSet(rule.Value, normalizeValue);
            if (values.Contains(wantedValue))
                return true;
        }

        return false;
    }

    private static bool EvaluateRule(
        RuleClause rule,
        Func<RuleClause, string, IEnumerable<string>> resolveFieldValues,
        Func<string, string> normalizeValue)
    {
        var fields = (rule.Field ?? string.Empty)
            .Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList();

        if (fields.Count == 0)
            return true;

        var resolvedValues = new List<string>();
        foreach (var field in fields)
            resolvedValues.AddRange(resolveFieldValues(rule, field));

        var fieldValues = NormalizeSet(resolvedValues, normalizeValue);
        var wantedValues = NormalizeSet(rule.Value, normalizeValue);

        return rule.Operator switch
        {
            RuleComparisonOp.In => wantedValues.Count > 0 && fieldValues.Overlaps(wantedValues),
            RuleComparisonOp.NotIn => wantedValues.Count == 0 || !fieldValues.Overlaps(wantedValues),
            RuleComparisonOp.Only => wantedValues.Count > 0
                                     && fieldValues.Count > 0
                                     && fieldValues.IsSubsetOf(wantedValues),
            _ => false
        };
    }

    private static HashSet<string> NormalizeSet(IEnumerable<string>? values, Func<string, string> normalize)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in values ?? Array.Empty<string>())
        {
            var normalized = normalize(raw ?? string.Empty);
            if (normalized.Length > 0)
                set.Add(normalized);
        }

        return set;
    }

    private static string DefaultNormalizeField(string value)
        => DefaultNormalizeValue(value);

    private static string DefaultNormalizeValue(string value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();
}
