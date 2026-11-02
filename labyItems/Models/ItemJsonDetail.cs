namespace labyItems.Models;

public class ItemJsonDetail
{
    public labyItems.Models.Items.MpEditorState? MpEditorState { get; set; }
    public List<string> Types { get; set; } = new();
    public List<CalcResult> Abilities { get; set; } = new();
    public string Status { get; set; } = "TODO"; // TODO: replace with real status model when available.
    public List<object> Modifiers { get; set; } = new(); // TODO: define Modifier model when it exists.
    public string DisplayName { get; set; } = string.Empty;
    public string PhysicalRepresentation { get; set; } = string.Empty;
    public string SourceFlow { get; set; } = string.Empty;
    public int MonsterPointCost { get; set; }
}
