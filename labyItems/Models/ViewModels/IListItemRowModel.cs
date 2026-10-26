using Microsoft.Maui.Graphics;

namespace labyItems.Models.ViewModels;

public interface IListItemRowModel
{
    string DisplayName { get; }
    string InlineSummary { get; }
    Color RowBackgroundColor { get; }
}
