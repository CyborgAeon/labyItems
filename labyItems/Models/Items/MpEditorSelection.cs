namespace labyItems.Models.Items;

public sealed class MpEditorSelection<TOption> where TOption : struct
{
    public TOption Option { get; set; }
    public int Uses { get; set; }
}
