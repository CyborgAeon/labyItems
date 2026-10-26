using Microsoft.Maui.Graphics;

namespace labyItems.Models.ViewModels;

public sealed class CharacterReviewAbilityRowVm
{
    public int Level { get; init; }
    public string LevelText => Level.ToString();
    public string Name { get; init; } = string.Empty;
    public string LookupKey { get; init; } = string.Empty;
    public Color RowBackgroundColor { get; init; } = Colors.White;
}
