namespace labyItems.Models;

public class IspCalculationResult
{
    public int TotalIsp { get; set; }
    public List<CalcResult> Abilities { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;
}
