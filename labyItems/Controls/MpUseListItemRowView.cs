using labyItems.Helpers;

namespace labyItems.Controls;

public sealed class MpUseListItemRowView : ListItemRowView
{
    public MpUseListItemRowView()
    {
        ShowFirstAction = true;
        FirstActionGlyph = FontAwesomeGlyphs.MinusSquare;
        ShowSecondAction = true;
        SecondActionGlyph = FontAwesomeGlyphs.PlusSquare;
        ShowInfo = true;
        ShowDelete = true;
    }
}
