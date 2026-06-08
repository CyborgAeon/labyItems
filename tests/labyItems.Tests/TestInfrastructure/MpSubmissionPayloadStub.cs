using labyItems.Models;

namespace labyItems.Pages.Calculator;

public sealed class MpSubmissionPayload
{
    public string ItemName { get; set; } = string.Empty;
    public string SourceFlow { get; set; } = "monster-point";
    public string PhysicalRepresentation { get; set; } = string.Empty;
    public List<string> ItemTypes { get; set; } = new();
    public List<CalcResult> Abilities { get; set; } = new();
    public int TotalIsp { get; set; }
    public int TotalMp { get; set; }
    public List<MpSubmissionBreakdownEntry>? Breakdown { get; set; }
    public List<MpSubmissionBreakdownEntry>? IspBreakdown { get; set; }
}

public sealed class MpSubmissionBreakdownEntry
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int RunningTotal { get; set; }
}
