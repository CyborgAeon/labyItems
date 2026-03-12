using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Configs;

public interface IConfigSelectionListItem
{
    string DisplayName { get; }
    string InlineSummary { get; }
    Color RowBackgroundColor { get; }
}
