using labyItems.Helpers;

namespace labyItems.Controls;

public sealed class EditableListItemRowView : ListItemRowView
{
    public EditableListItemRowView()
    {
        ShowFirstAction = true;
        FirstActionGlyph = FontAwesomeGlyphs.Edit;
        ShowInfo = true;
        ShowDelete = true;
    }
}
