using labyItems.Services;

namespace labyItems.Models.NonStandard;

public sealed class RaceLifeScaleClassEntry
{
    public string ClassName { get; set; } = string.Empty;
    public IReadOnlyList<LifeScalePoint>? Points { get; set; }
}
