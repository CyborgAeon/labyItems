using labyItems.Services;

namespace labyItems.Models.NonStandard;

public sealed class NonStandardLifeScaleAssignment
{
    public string RaceName { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public IReadOnlyList<LifeScalePoint>? Points { get; set; }
}
