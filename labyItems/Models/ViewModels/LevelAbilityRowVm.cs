using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Models.Characters;

namespace labyItems.Models.ViewModels;

public sealed class LevelAbilityRowVm
{
    public int Level { get; set; }
    public bool IsTableStage { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Loc { get; set; } = string.Empty;
    public string WeaponSkills { get; set; } = string.Empty;
    public IReadOnlyList<string> AbilityNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AbilityDetailKeys { get; set; } = Array.Empty<string>();

    public string StageText => IsTableStage ? $"T{Level}" : Level.ToString();

    public string AbilitiesText => string.Join(", ", AbilityNames
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Select(name => name.Trim()));

    public bool HasAbilityDetails =>
        AbilityNames.Any(name => !string.IsNullOrWhiteSpace(name))
        || AbilityDetailKeys.Any(key => !string.IsNullOrWhiteSpace(key));
}

public static class LevelAbilityRowBuilder
{
    private static readonly HashSet<string> WeaponSkillCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "O",
        "B",
        "E",
        "UAC",
        "H",
        "MP",
        "U"
    };

    private static readonly Regex WeaponSkillTokenRegex = new(
        "[A-Za-z]+",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled));

    public static LevelAbilityRowVm Build(
        int level,
        IEnumerable<AbilityDefinition>? abilityDefinitions,
        string? body = null,
        string? loc = null,
        bool isTableStage = false)
    {
        var weaponSkillCodes = new List<string>();
        var abilityNames = new List<string>();
        var abilityDetailKeys = new List<string>();

        foreach (var definition in abilityDefinitions ?? Enumerable.Empty<AbilityDefinition>())
        {
            if (TryExtractWeaponSkillCode(definition, out var weaponCode))
            {
                AddUnique(weaponSkillCodes, weaponCode);
                continue;
            }

            var displayName = ToDisplayName(definition);
            if (displayName.Length > 0)
                AddUnique(abilityNames, displayName);

            var detailKey = (definition?.Key ?? string.Empty).Trim();
            if (detailKey.Length == 0)
                detailKey = displayName;
            if (detailKey.Length > 0)
                AddUnique(abilityDetailKeys, detailKey);
        }

        return new LevelAbilityRowVm
        {
            Level = level,
            IsTableStage = isTableStage,
            Body = (body ?? string.Empty).Trim(),
            Loc = (loc ?? string.Empty).Trim(),
            WeaponSkills = string.Join(", ", weaponSkillCodes),
            AbilityNames = abilityNames,
            AbilityDetailKeys = abilityDetailKeys
        };
    }

    public static string ToDisplayName(AbilityDefinition? definition)
    {
        if (definition == null)
            return string.Empty;

        var name = (definition.Name ?? string.Empty).Trim();
        if (name.Length > 0)
            return name;

        return (definition.Effect ?? string.Empty).Trim();
    }

    private static bool TryExtractWeaponSkillCode(AbilityDefinition? definition, out string code)
    {
        code = string.Empty;

        if (!string.Equals((definition?.Type ?? string.Empty).Trim(), "WeaponSkill", StringComparison.OrdinalIgnoreCase))
            return false;

        var raw = (definition?.Name ?? string.Empty).Trim();
        if (raw.Length == 0)
            return false;

        if (WeaponSkillCodes.Contains(raw))
        {
            code = raw.ToUpperInvariant();
            return true;
        }

        var tokens = WeaponSkillTokenRegex
            .Matches(raw.ToUpperInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length > 0)
            .ToList();

        if (tokens.Count == 0)
            return false;

        if (tokens.Any(token => !WeaponSkillCodes.Contains(token)))
            return false;

        code = string.Join("/", tokens.Distinct(StringComparer.OrdinalIgnoreCase));
        return code.Length > 0;
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (value.Length == 0)
            return;

        if (values.Any(existing => existing.Equals(value, StringComparison.OrdinalIgnoreCase)))
            return;

        values.Add(value);
    }
}
