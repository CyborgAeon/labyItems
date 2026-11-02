namespace labyItems.Models.WorldMap;

public sealed record WorldStatsPeriod(int Year, int Month)
{
    public override string ToString() => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
}
