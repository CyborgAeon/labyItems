using System.Text.RegularExpressions;
using labyItems.Models;

namespace labyItems.Helpers;

public static class NotationHelper
{
    private static readonly HashSet<string> InvariantNumericLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC",
        "PAC",
        "DAC",
        "MAC",
        "SAC"
    };

    public static string ToKNotation(this int number) => Format(number);

    public static string ToKNotation(this long number) => Format(number);

    public static string BuildGrantSummary(IEnumerable<CalcResult>? abilities)
    {
        var abilityText = JoinWithAnd(BuildHumanReadableAbilityLines(abilities));
        return abilityText.Length == 0 ? string.Empty : $"Grants {abilityText}";
    }

    public static List<string> BuildHumanReadableAbilityLines(IEnumerable<CalcResult>? abilities)
    {
        var lines = new List<string>();
        foreach (var ability in abilities ?? Enumerable.Empty<CalcResult>())
        {
            var detailLines = BuildHumanReadableAbilityTextFromDetails(ability);
            if (detailLines.Count > 0)
            {
                lines.AddRange(detailLines);
                continue;
            }

            var rawText = !string.IsNullOrWhiteSpace(ability.Summary)
                ? ability.Summary
                : ability.AbilityName;

            AddHumanReadableAbilityText(lines, rawText);
        }

        return lines;
    }

    public static List<string> BuildHumanReadableAbilityLinesFromText(string? rawText)
    {
        var lines = new List<string>();
        AddHumanReadableAbilityText(lines, rawText);
        return lines;
    }

    private static string Format(long number)
    {
        if (number < 1000)
            return number.ToString();

        long rounded = number / 1000; // rounds to nearest 1000
        return $"{rounded}k";
    }

    private static List<string> BuildHumanReadableAbilityTextFromDetails(CalcResult ability)
    {
        var lines = new List<string>();
        AddHumanReadableDetailRows(lines, ability.Details, "spells", "spellName");
        AddHumanReadableDetailRows(lines, ability.Details, "miracles", "miracleName");
        AddHumanReadableDetailRows(lines, ability.Details, "evocations", "evocationName");
        AddHumanReadableDetailRows(lines, ability.Details, "selectedGeneralAbilities", "name");
        if (lines.Count > 0)
            return lines;

        if (HasAbilityType(ability, "Spell", "Miracle", "Evocation"))
            return lines;

        AddGenericHumanReadableDetailRows(lines, ability);
        return lines;
    }

    private static void AddGenericHumanReadableDetailRows(List<string> lines, CalcResult ability)
    {
        var type = (ability.AbilityType ?? string.Empty).Trim();
        var name = StripPlaceholderAbilityName(type, ability.AbilityName);

        if (type.Equals("Life", StringComparison.OrdinalIgnoreCase))
        {
            var life = ReadProperty(ability.Details, "life");
            if (life.StartsWith("No additional", StringComparison.OrdinalIgnoreCase))
                return;

            AddIfPresent(lines, life.Length > 0 ? $"{life} life" : name);
            return;
        }

        if (type.Equals("More", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Utility", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var part in SplitUtilitySummary(ability.Summary))
                AddIfPresent(lines, part);
            return;
        }

        AddIfPresent(lines, name);
        AddIfPresent(lines, ReadProperty(ability.Details, "layeredSummary"));

        AddNumericDetail(lines, ability.Details, "AC", "AC");
        AddNumericDetail(lines, ability.Details, "PAC", "PAC");
        AddNumericDetail(lines, ability.Details, "DAC", "DAC");
        AddNumericDetail(lines, ability.Details, "MAC", "MAC");
        AddNumericDetail(lines, ability.Details, "SAC", "SAC");

        AddNumericDetail(lines, ability.Details, "shieldColours", "shield colour");
        AddNumericDetail(lines, ability.Details, "shieldAlignments", "shield alignment");
        AddNumericDetail(lines, ability.Details, "magicalColours", "magical colour");
        AddNumericDetail(lines, ability.Details, "macColours", "MAC colour");
        AddNumericDetail(lines, ability.Details, "sacAlignment", "SAC alignment");

        AddEnhancementRows(lines, ability.Details);
    }

    private static void AddEnhancementRows(List<string> lines, IReadOnlyDictionary<string, object?> details)
    {
        if (!details.TryGetValue("enhancementBonuses", out var rawRows) || rawRows is string)
            return;

        if (rawRows is not System.Collections.IEnumerable rows)
            return;

        foreach (var row in rows)
        {
            var type = ReadProperty(row, "type");
            var value = ReadIntProperty(row, "value");
            if (type.Length > 0 && value > 0)
                AddIfPresent(lines, $"{value} {type}");
        }
    }

    private static void AddNumericDetail(List<string> lines, IReadOnlyDictionary<string, object?> details, string key, string label)
    {
        var value = ReadIntProperty(details, key);
        if (value <= 0)
            return;

        var suffix = value == 1 || InvariantNumericLabels.Contains(label)
            ? label
            : Pluralize(label);
        AddIfPresent(lines, $"{value} {suffix}");
    }

    private static string Pluralize(string label)
        => label.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? label : $"{label}s";

    private static void AddIfPresent(List<string> lines, string? value)
    {
        var text = NormalizeWhitespace(value);
        if (text.Length > 0 && !lines.Contains(text, StringComparer.OrdinalIgnoreCase))
            lines.Add(text);
    }

    private static string StripPlaceholderAbilityName(string abilityType, string? abilityName)
    {
        var name = NormalizeWhitespace(abilityName);
        if (name.Length == 0)
            return string.Empty;

        var type = NormalizeWhitespace(abilityType);
        if (name.Equals(type, StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        if (name.StartsWith("No ", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        if (name.EndsWith(" not configured yet.", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return name;
    }

    private static IEnumerable<string> SplitUtilitySummary(string? summary)
    {
        foreach (var part in (summary ?? string.Empty).Split('\u00B7', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var text = NormalizeWhitespace(part);
            if (text.Length == 0)
                continue;
            if (text.StartsWith("No additional modifiers", StringComparison.OrdinalIgnoreCase))
                continue;
            if (text.StartsWith("Delta ISP", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("\u0394 ISP", StringComparison.OrdinalIgnoreCase))
                continue;

            yield return text;
        }
    }

    private static void AddHumanReadableDetailRows(
        List<string> lines,
        IReadOnlyDictionary<string, object?> details,
        string detailKey,
        string nameProperty)
    {
        if (!details.TryGetValue(detailKey, out var rawRows) || rawRows is string)
            return;

        if (rawRows is not System.Collections.IEnumerable rows)
            return;

        foreach (var row in rows)
        {
            var name = ReadProperty(row, nameProperty);
            if (name.Length == 0)
                name = ReadProperty(row, "name");
            if (name.Length == 0)
                continue;

            var uses = ReadIntProperty(row, "basicPerDay") + ReadIntProperty(row, "advancedPerDay");
            lines.Add(uses > 0 ? $"{name} {uses}/day" : name);
        }
    }

    private static void AddHumanReadableAbilityText(List<string> lines, string? rawText)
    {
        foreach (var part in SplitAbilityText(rawText))
        {
            var text = ConvertToHumanReadableAbilityText(part);
            if (text.Length > 0)
                lines.Add(text);
        }
    }

    private static IEnumerable<string> SplitAbilityText(string? rawText)
    {
        var text = NormalizeWhitespace(rawText);
        if (text.Length == 0)
            yield break;

        foreach (var part in Regex.Split(text, @"\s*,\s*(?=(?:Spell|Miracle|Evocation)\s*:)", RegexOptions.IgnoreCase))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                yield return trimmed;
        }
    }

    private static string ConvertToHumanReadableAbilityText(string? rawText)
    {
        var text = NormalizeWhitespace(rawText);
        if (text.Length == 0)
            return string.Empty;

        text = Regex.Replace(text, @"\s*=\s*-?\d+\s*$", string.Empty).Trim();
        text = Regex.Replace(text, @"^(?:Spell|Miracle|Evocation)\s*:\s*", string.Empty, RegexOptions.IgnoreCase).Trim();

        var useMatch = Regex.Match(text, @"^(?<name>.+?)\s+x(?<uses>\d+)\s*$", RegexOptions.IgnoreCase);
        if (useMatch.Success)
        {
            var abilityName = StripTrailingAbilityMetadata(useMatch.Groups["name"].Value);
            return int.TryParse(useMatch.Groups["uses"].Value, out var uses) && uses > 0 && abilityName.Length > 0
                ? $"{abilityName} {uses}/day"
                : abilityName;
        }

        return StripTrailingAbilityMetadata(text);
    }

    private static string StripTrailingAbilityMetadata(string? value)
    {
        var text = NormalizeWhitespace(value);
        if (text.Length == 0)
            return string.Empty;

        return Regex.Replace(
                text,
                @"\s*\((?:(?:lvl\s*)?\d+[^)]*|[^)]*\b(?:handbook|advanced|adv)\b[^)]*)\)\s*$",
                string.Empty,
                RegexOptions.IgnoreCase)
            .Trim();
    }

    private static string JoinWithAnd(IEnumerable<string>? values)
    {
        var parts = values?
            .Select(NormalizeWhitespace)
            .Where(value => value.Length > 0)
            .ToList() ?? new List<string>();

        return parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}"
        };
    }

    private static string NormalizeWhitespace(string? value)
        => Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ");

    private static bool HasAbilityType(CalcResult ability, params string[] typeNames)
    {
        var type = (ability.AbilityType ?? string.Empty).Trim();
        return typeNames.Any(candidate => type.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadProperty(object? instance, string propertyName)
    {
        if (instance == null)
            return string.Empty;

        if (instance is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(propertyName, out var readOnlyValue))
        {
            return (readOnlyValue?.ToString() ?? string.Empty).Trim();
        }

        if (instance is System.Collections.IDictionary dictionary
            && dictionary.Contains(propertyName))
        {
            return (dictionary[propertyName]?.ToString() ?? string.Empty).Trim();
        }

        var property = instance.GetType().GetProperty(propertyName);
        return (property?.GetValue(instance)?.ToString() ?? string.Empty).Trim();
    }

    private static int ReadIntProperty(object? instance, string propertyName)
    {
        var value = ReadProperty(instance, propertyName);
        return int.TryParse(value, out var parsed) ? Math.Max(0, parsed) : 0;
    }
}
