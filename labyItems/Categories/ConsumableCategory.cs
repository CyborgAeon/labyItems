using labyItems.Models;
namespace labyItems.Categories;

public sealed class ConsumableCategory : CategoryBase
{
    public List<CalcRow> Rows { get; } = new();
    public ConsumableCategory()
    {
        // TODO: fill rows
    }

    public override (int TotalIsp, List<string> Lines) AddToSummary()
    {
        var total = SumRows(Rows);
        if (total > 0) total = (int)(Math.Round(total / 5.0, MidpointRounding.AwayFromZero) * 5);
        var lines = LinesFrom(Rows);
        return (total, lines);
    }
}
