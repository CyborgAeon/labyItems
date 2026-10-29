namespace labyItems.Models.ViewModels;

public sealed class CharacterReviewLifeScaleRowVm
{
    public int Level { get; init; }
    public string LevelText => Level.ToString();
    public string Body { get; init; } = "—";
    public string Loc { get; init; } = "—";
    public string ValueText => $"{Body}/{Loc}";
    public bool ShowDivider => Level > 1;
}
