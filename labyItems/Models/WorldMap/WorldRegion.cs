namespace labyItems.Models.WorldMap;

public sealed class WorldRegion
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public IReadOnlyList<string> RelatedStatisticsIds { get; init; } = [];
    public string? StatisticsId { get; init; }
}
