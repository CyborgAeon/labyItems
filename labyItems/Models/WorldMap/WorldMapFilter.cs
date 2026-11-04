namespace labyItems.Models.WorldMap;

public sealed record WorldMapFilter(string Query, IReadOnlyList<WorldStatisticFilter> Criteria)
{
    public bool IsActive => !string.IsNullOrWhiteSpace(Query) || Criteria.Count > 0;
    public static WorldMapFilter Empty { get; } = new("", []);
}
