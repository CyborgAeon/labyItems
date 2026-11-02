using labyItems.Models;
using labyItems.Models.Items;
using labyItems.Controls;

namespace labyItems.Models.Items;

public class MpSubmissionPayload
{
    public MpEditorState? EditorState { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string SourceFlow { get; set; } = "monster-point";
    public string PhysicalRepresentation { get; set; } = string.Empty;
    public List<string> ItemTypes { get; set; } = new();
    public List<CalcResult> Abilities { get; set; } = new();
    public int TotalMp { get; set; }
    public List<ContributionRow> Breakdown { get; set; } = new();
    public int TotalIsp { get; set; }
    public List<ContributionRow> IspBreakdown { get; set; } = new();
}
