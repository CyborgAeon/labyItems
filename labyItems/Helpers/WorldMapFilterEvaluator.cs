using labyItems.Models.WorldMap;

namespace labyItems.Helpers;

public static class WorldMapFilterEvaluator
{
    public static IReadOnlyList<WorldTownResult> Apply(IEnumerable<WorldTownResult>? towns, WorldMapFilter filter) =>
        (towns ?? []).Where(town => Matches(town, filter)).OrderBy(town => town.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public static bool Matches(WorldTownResult town, WorldMapFilter filter)
    {
        if (!town.Name.Contains(filter.Query.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var criterion in filter.Criteria)
        {
            var values = (town.Statistics ?? []).Where(stat => stat.Category == criterion.Category && (criterion.Name is null || stat.Name == criterion.Name)).Select(stat => stat.Value).ToList();
            // Unpublished values are unknown, including values missing in historical months.
            if (values.Count == 0) return false;
            var value = values.Max();
            if (criterion.Minimum is { } minimum && value < minimum) return false;
            if (criterion.Maximum is { } maximum && value > maximum) return false;
        }
        return true;
    }
}
