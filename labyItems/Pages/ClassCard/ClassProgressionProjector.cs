using labyItems.Models.Characters;

namespace labyItems.Pages.ClassCard;

public static class ClassProgressionProjector
{
    public static IReadOnlyList<ClassProgressionRow> BuildProgressionRows(
        IReadOnlyDictionary<string, List<AbilityDefinition>>? levels)
    {
        var rows = new List<ClassProgressionRow>();
        foreach (var entry in OrderedLevels(levels))
        {
            if (!TryStage(entry.Key, out var level))
                continue;

            var definitions = (entry.Value ?? [])
                .Where(ability => ability != null && !IsWeaponSkill(ability))
                .ToList();
            var armour = definitions
                .Where(IsArmourGrant)
                .Select(ability => $"{ability.Count} {ability.Type.ToUpperInvariant()}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var abilities = definitions.Where(ability => !IsArmourGrant(ability)).ToList();
            var masteries = abilities
                .Where(ability => ability.Name.Contains("Weapon Mastery", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var remaining = abilities.Except(masteries).ToList();

            if (masteries.Count > 0)
            {
                rows.Add(new ClassProgressionRow(level, masteries, string.Join(", ", armour)));
                if (remaining.Count > 0)
                    rows.Add(new ClassProgressionRow(level, remaining, string.Empty));
            }
            else
            {
                var armourText = string.Join(", ", armour);
                if (remaining.Count > 0 || armourText.Length > 0)
                    rows.Add(new ClassProgressionRow(level, remaining, armourText));
            }
        }

        return rows;
    }

    public static IReadOnlyList<ClassProgressionRow> BuildWeaponSkillRows(
        IReadOnlyDictionary<string, List<AbilityDefinition>>? levels)
    {
        var rows = new List<ClassProgressionRow>();
        foreach (var entry in OrderedLevels(levels))
        {
            if (!TryStage(entry.Key, out var level))
                continue;

            rows.AddRange((entry.Value ?? [])
                .Where(ability => ability != null && IsWeaponSkill(ability))
                .Select(ability => new ClassProgressionRow(level, ability)));
        }

        return rows;
    }

    public static string BuildWeaponSkillSummary(IEnumerable<ClassProgressionRow> rows)
    {
        var items = rows
            .Select(row => row.Name)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return items.Count switch
        {
            0 => string.Empty,
            1 => items[0],
            2 => $"{items[0]} & {items[1]}",
            _ => $"{string.Join(", ", items.Take(items.Count - 1))} & {items[^1]}"
        };
    }

    private static IEnumerable<KeyValuePair<string, List<AbilityDefinition>>> OrderedLevels(
        IReadOnlyDictionary<string, List<AbilityDefinition>>? levels) =>
        (levels ?? new Dictionary<string, List<AbilityDefinition>>()).OrderBy(entry => StageSort(entry.Key));

    private static bool IsWeaponSkill(AbilityDefinition ability) =>
        ability.Type.Contains("WeaponSkill", StringComparison.OrdinalIgnoreCase)
        || ability.Type.Contains("Weapon Skill", StringComparison.OrdinalIgnoreCase);

    private static bool IsArmourGrant(AbilityDefinition ability)
    {
        var type = ability.Type.Trim();
        return ability.Count.HasValue
            && type.Length is 2 or 3
            && type.EndsWith("AC", StringComparison.OrdinalIgnoreCase);
    }

    private static int StageSort(string value) =>
        int.TryParse(new string(value.Where(char.IsDigit).ToArray()), out var number) ? number : int.MaxValue;

    private static bool TryStage(string value, out int level) =>
        int.TryParse(new string(value.Where(char.IsDigit).ToArray()), out level);
}

public sealed class ClassProgressionRow
{
    public ClassProgressionRow(int level, AbilityDefinition definition)
        : this(level, new[] { definition }, string.Empty) { }

    public ClassProgressionRow(int level, IReadOnlyList<AbilityDefinition> definitions, string armourText)
    {
        Level = level;
        Definitions = definitions;
        ArmourText = armourText;
    }

    public int Level { get; }
    public string LevelText => Level.ToString();
    public IReadOnlyList<AbilityDefinition> Definitions { get; }
    public AbilityDefinition Definition => Definitions.FirstOrDefault() ?? new AbilityDefinition();
    public string Name => string.Join(", ", Definitions
        .Select(definition => definition.Name)
        .Where(name => !string.IsNullOrWhiteSpace(name)));
    public string ArmourText { get; }
    public bool HasDetails => Definitions.Any(definition =>
        !string.IsNullOrWhiteSpace(definition.Name)
        || !string.IsNullOrWhiteSpace(definition.AbilityRef)
        || !string.IsNullOrWhiteSpace(definition.Key));
}

public sealed record ClassWeaponSkillLevelRow(int Level, string Names)
{
    public string LevelText => Level.ToString();
}
