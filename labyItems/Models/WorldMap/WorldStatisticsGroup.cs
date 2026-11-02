namespace labyItems.Models.WorldMap;

public sealed record WorldStatisticsGroup(string Place, IReadOnlyList<WorldStatistic> Statistics);
