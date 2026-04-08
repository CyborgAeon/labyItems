using System.Text.RegularExpressions;

namespace labyItems.Helpers;

public enum ProgressionStageKind
{
    Unknown = 0,
    Level = 1,
    Table = 2
}

public readonly record struct ProgressionStage(ProgressionStageKind Kind, int Value)
{
    public bool IsValid => Kind != ProgressionStageKind.Unknown && Value > 0;

    public int SortBucket => Kind switch
    {
        ProgressionStageKind.Level => 0,
        ProgressionStageKind.Table => 1,
        _ => 2
    };

    public string DisplayLabel => !IsValid
        ? string.Empty
        : Kind == ProgressionStageKind.Table
            ? $"Tbl {Value}"
            : $"Lvl {Value}";
}

public static class CharacterProgressionTables
{
    private static readonly int[] TablePointThresholds =
    {
        0, 200, 250, 275, 450, 600, 650, 1000, 1500, 3000, 5250, 7500
    };

    private static readonly Regex TableStageRegex = new(
        @"^(?:table|tbl|t)\s*[:#-]?\s*(\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LevelStageRegex = new(
        @"^(?:level|lvl|l)\s*[:#-]?\s*(\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnyDigitsRegex = new(
        @"\d+",
        RegexOptions.Compiled);

    public static int MaxTable => TablePointThresholds.Length;

    public static int GetHighestTableReached(int points)
    {
        var resolvedPoints = Math.Max(0, points);
        for (var i = TablePointThresholds.Length - 1; i >= 0; i--)
        {
            if (resolvedPoints >= TablePointThresholds[i])
                return i + 1;
        }

        return 1;
    }

    public static bool HasReachedTable(int points, int table)
        => GetHighestTableReached(points) >= Math.Max(1, table);

    public static int GetPointsThresholdForTable(int table)
    {
        if (table <= 1)
            return 0;

        if (table > MaxTable)
            return int.MaxValue;

        return TablePointThresholds[table - 1];
    }

    public static bool TryParseStage(string? rawKey, out ProgressionStage stage)
    {
        stage = default;

        var key = (rawKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return false;

        if (TryParseExactTableStage(key, out var table))
        {
            stage = new ProgressionStage(ProgressionStageKind.Table, table);
            return true;
        }

        if (TryParseExactLevelStage(key, out var level))
        {
            stage = new ProgressionStage(ProgressionStageKind.Level, level);
            return true;
        }

        if (int.TryParse(key, out var numeric))
        {
            if (numeric <= 0)
                return false;

            stage = new ProgressionStage(
                numeric <= 8 ? ProgressionStageKind.Level : ProgressionStageKind.Table,
                numeric);
            return true;
        }

        var hasTableToken = key.Contains("table", StringComparison.OrdinalIgnoreCase)
                            || key.StartsWith("t", StringComparison.OrdinalIgnoreCase);
        var hasLevelToken = key.Contains("level", StringComparison.OrdinalIgnoreCase)
                            || key.Contains("lvl", StringComparison.OrdinalIgnoreCase);

        var digitsMatch = AnyDigitsRegex.Match(key);
        if (!digitsMatch.Success || !int.TryParse(digitsMatch.Value, out var embeddedNumber) || embeddedNumber <= 0)
            return false;

        var kind = hasTableToken
            ? ProgressionStageKind.Table
            : hasLevelToken
                ? ProgressionStageKind.Level
                : embeddedNumber <= 8
                    ? ProgressionStageKind.Level
                    : ProgressionStageKind.Table;

        stage = new ProgressionStage(kind, embeddedNumber);
        return true;
    }

    private static bool TryParseExactTableStage(string key, out int value)
    {
        value = 0;
        var match = TableStageRegex.Match(key);
        return match.Success
               && int.TryParse(match.Groups[1].Value, out value)
               && value > 0;
    }

    private static bool TryParseExactLevelStage(string key, out int value)
    {
        value = 0;
        var match = LevelStageRegex.Match(key);
        return match.Success
               && int.TryParse(match.Groups[1].Value, out value)
               && value > 0;
    }
}
