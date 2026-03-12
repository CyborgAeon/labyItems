using labyItems.Models.Characters;
using labyItems.Models.Rules;

namespace labyItems.Services;

public sealed class AbilityAvailabilityService : IAbilityAvailabilityService
{
    public bool IsAvailable(
        IReadOnlyList<RuleClause>? rules,
        CharacterDraft draft,
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        IReadOnlyDictionary<string, PeopleRecord> races)
    {
        var list = (rules ?? Array.Empty<RuleClause>())
            .Where(rule => rule != null && rule.IsValid)
            .ToList();
        if (list.Count == 0)
            return true;

        var className = (draft?.Class ?? string.Empty).Trim();
        var raceName = (draft?.Race ?? string.Empty).Trim();
        var classRecord = ResolveClassRecord(classes, className);
        var classBrackets = ExpandClassBrackets(classRecord?.Brackets);
        var peopleTypes = ResolvePeopleTypesForRace(races, raceName, draft);
        var alignmentOrder = draft?.Alignment?.Order.ToString() ?? string.Empty;
        var alignmentMoral = draft?.Alignment?.Moral.ToString() ?? string.Empty;

        IEnumerable<string> ResolveValues(string field)
        {
            var normalizedField = NormalizeRuleField(field);
            return normalizedField switch
            {
                "class" or "classes" => ToSingleValue(className),
                "bracket" or "brackets" => classBrackets,
                "race" or "races" => ToSingleValue(raceName),
                "baserace" or "baseraces" => ToSingleValue(raceName),
                "peopletype" or "peopletypes" => peopleTypes,
                "alignmentorder" => ToSingleValue(alignmentOrder),
                "alignmentmoral" => ToSingleValue(alignmentMoral),
                "status" or "statuses" => Array.Empty<string>(),
                _ => Array.Empty<string>()
            };
        }

        return RuleTreeEvaluator.Evaluate(list, ResolveValues, NormalizeRuleField);
    }

    private static CharacterClassRecord? ResolveClassRecord(
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        string className)
    {
        if (className.Length == 0 || classes.Count == 0)
            return null;

        if (classes.TryGetValue(className, out var direct))
            return direct;

        var normalizedClassName = NormalizeClassKey(className);
        foreach (var pair in classes)
        {
            if (string.Equals(pair.Key, className, StringComparison.OrdinalIgnoreCase))
                return pair.Value;

            if (NormalizeClassKey(pair.Key) == normalizedClassName)
                return pair.Value;
        }

        foreach (var pair in classes)
        {
            if (className.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;

            var normalizedKey = NormalizeClassKey(pair.Key);
            if (normalizedClassName.EndsWith(normalizedKey, StringComparison.Ordinal))
                return pair.Value;
        }

        return null;
    }

    private static string NormalizeClassKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return new string(raw.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static IReadOnlyList<string> ExpandClassBrackets(IEnumerable<string>? brackets)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bracket in brackets ?? Enumerable.Empty<string>())
        {
            var trimmed = (bracket ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                continue;

            set.Add(trimmed);
            var normalized = NormalizeRuleField(trimmed);
            if (normalized.Length > 0)
                set.Add(normalized);
        }

        return set.ToList();
    }

    private static IReadOnlyList<string> ResolvePeopleTypesForRace(
        IReadOnlyDictionary<string, PeopleRecord> races,
        string raceName,
        CharacterDraft? draft)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (raceName.Length == 0 || races.Count == 0)
            return ApplyBarbarianPeopleType(resolved, draft).ToList();

        if (races.TryGetValue(raceName, out var record))
        {
            foreach (var type in NormalizePeopleTypes(record))
                resolved.Add(type);

            return ApplyBarbarianPeopleType(resolved, draft).ToList();
        }

        var match = races.FirstOrDefault(pair =>
            string.Equals((pair.Key ?? string.Empty).Trim(), raceName, StringComparison.OrdinalIgnoreCase));

        if (match.Value != null)
        {
            foreach (var type in NormalizePeopleTypes(match.Value))
                resolved.Add(type);
        }

        return ApplyBarbarianPeopleType(resolved, draft).ToList();
    }

    private static IReadOnlyList<string> NormalizePeopleTypes(PeopleRecord record)
    {
        return (record?.PeopleType ?? new List<string>())
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> ApplyBarbarianPeopleType(HashSet<string> peopleTypes, CharacterDraft? draft)
    {
        if (HasBarbarianPeopleType(draft))
            peopleTypes.Add("Tribal");

        return peopleTypes;
    }

    private static bool HasBarbarianPeopleType(CharacterDraft? draft)
    {
        if (draft?.SpecialisationSelections == null || draft.SpecialisationSelections.Count == 0)
            return false;

        if (draft.SpecialisationSelections.TryGetValue("Barbarian", out var directSelection)
            && string.Equals((directSelection ?? string.Empty).Trim(), "Barbarian", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return draft.SpecialisationSelections.Values.Any(selection =>
            string.Equals((selection ?? string.Empty).Trim(), "Barbarian", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ToSingleValue(string value)
        => string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value.Trim() };

    private static string NormalizeRuleField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Trim().Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
