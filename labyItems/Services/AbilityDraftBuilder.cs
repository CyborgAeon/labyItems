using System.Text.RegularExpressions;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class AbilityDraftBuilder
{
    private static readonly char[] _listSeparators = { ',', '&', '/', '+', ';' };
    private static readonly char[] _weaponSeparators = { ',', '&', '/', '+', ';', ' ' };
    private static readonly Regex _perLevelRegex = new(
        @"\b(per\s+levels?|\d+\s*/\s*\d+\s*levels?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<AbilityDraft> BuildFromLevels(Dictionary<string, List<string>> levels)
    {
        var list = new List<AbilityDraft>();
        foreach (var kvp in levels ?? new Dictionary<string, List<string>>())
        {
            var key = (kvp.Key ?? string.Empty).Trim();
            int? level = int.TryParse(key, out var parsed) ? parsed : null;
            foreach (var ability in kvp.Value ?? new List<string>())
                list.AddRange(ParseAbility(ability, level));
        }

        return list;
    }

    public static List<AbilityDraft> ParseAbility(string rawAbility, int? levelGained)
    {
        var result = new List<AbilityDraft>();

        if (string.IsNullOrWhiteSpace(rawAbility))
            return result;

        var ability = rawAbility.Trim();

        if (TryParseWeaponCodes(ability, out var codes))
        {
            foreach (var code in codes)
            {
                result.Add(new AbilityDraft
                {
                    Name = code,
                    AbilityType = AbilityType.WeaponSkill,
                    LevelGained = levelGained,
                    ShortStringValue = ""
                });
            }

            return result;
        }

        var type = DetermineAbilityType(ability);
        foreach (var name in SplitAbilityNames(ability, type))
        {
            result.Add(new AbilityDraft
            {
                Name = name,
                AbilityType = type,
                LevelGained = levelGained,
                ShortStringValue = ""
            });
        }

        return result;
    }

    private static bool TryParseWeaponCodes(string ability, out List<string> codes)
    {
        var tokens = ability.Split(_weaponSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .ToList();

        codes = new List<string>();
        if (tokens.Count == 0)
            return false;

        foreach (var token in tokens)
        {
            var upper = token.ToUpperInvariant();
            if (upper is "O" or "E" or "U" or "B" or "MP" or "H")
            {
                codes.Add(token);
                continue;
            }

            codes.Clear();
            return false;
        }

        // preserve order but avoid accidental repeats such as "O & O"
        codes = codes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return codes.Count > 0;
    }

    private static AbilityType DetermineAbilityType(string ability)
    {
        var lower = ability.ToLowerInvariant();

        if (_perLevelRegex.IsMatch(lower)
            || lower.Contains("/level", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("/ level", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("/levels", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("/ levels", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("per level", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("per levels", StringComparison.OrdinalIgnoreCase))
            return AbilityType.Innate;

        if (lower.Contains("innate", StringComparison.OrdinalIgnoreCase))
            return AbilityType.Innate;

        if (lower.Contains("immunity", StringComparison.OrdinalIgnoreCase))
            return AbilityType.Immunity;

        if (lower.Contains("resistance", StringComparison.OrdinalIgnoreCase))
            return AbilityType.Resistance;

        if (lower.Contains("at will", StringComparison.OrdinalIgnoreCase) || lower.Contains("@will", StringComparison.OrdinalIgnoreCase))
            return AbilityType.AtWill;

        if (lower.Contains("armour", StringComparison.OrdinalIgnoreCase) || lower.Contains("armor", StringComparison.OrdinalIgnoreCase))
            return AbilityType.Static;

        return AbilityType.Static;
    }

    private static IEnumerable<string> SplitAbilityNames(string ability, AbilityType type)
    {
        if (type == AbilityType.Immunity)
            return SplitByPrefix(ability, "immunity to");

        if (type == AbilityType.Resistance)
            return SplitByPrefix(ability, "resistance to");

        return new[] { ability };
    }

    private static IEnumerable<string> SplitByPrefix(string ability, string prefixKey)
    {
        var idx = ability.IndexOf(prefixKey, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var prefix = ability.Substring(0, idx + prefixKey.Length).Trim();
            var remainder = ability.Substring(idx + prefixKey.Length).Trim().TrimStart(':');
            var items = SplitListItems(remainder);

            if (items.Count > 1)
                return items.Select(i => $"{prefix} {i}".Trim());
        }

        var split = SplitListItems(ability);
        return split.Count > 1 ? split : new[] { ability };
    }

    private static List<string> SplitListItems(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var normalized = text.Replace(" and ", ",", StringComparison.OrdinalIgnoreCase);

        var parts = normalized.Split(_listSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        return parts.Count == 0 ? new List<string> { text.Trim() } : parts;
    }
}
