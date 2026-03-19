namespace labyItems.Pages.Calculator;

public sealed class MpSubmissionPayload
{
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
