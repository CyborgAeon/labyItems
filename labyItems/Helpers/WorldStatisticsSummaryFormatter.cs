using labyItems.Models.WorldMap;

namespace labyItems.Helpers;

public static class WorldStatisticsSummaryFormatter
{
    public static string HighestText(IEnumerable<WorldStatisticsGroup>? groups, string category)
    {
        var available = (groups ?? []).ToList();
        var top = available.SelectMany(group => (group.Statistics ?? []).Where(stat => stat.Category == category)
                .Select(stat => (group.Place, Statistic: stat)))
            .OrderByDescending(item => item.Statistic.Value)
            .ThenBy(item => item.Statistic.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Place, StringComparer.Ordinal)
            .FirstOrDefault();
        if (top.Statistic is null) return "No published data";
        var source = available.Count > 1 ? $" · {top.Place}" : "";
        return $"{top.Statistic.Name} · {top.Statistic.Value:0.##}{source}";
    }
}
