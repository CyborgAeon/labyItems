namespace labyItems.Models.WorldMap;

public sealed record WorldTownResult(WorldRegion Region, IReadOnlyList<WorldStatistic> Statistics)
{
    public string Name => Region.Name;
    public string Summary => Statistics.FirstOrDefault(s => s.Category == "Administration" && s.Name == "Volksmight") is { } hall
        ? $"Hiring Hall score · {hall.Value:0.##}" : "No published Hiring Hall score";
}
