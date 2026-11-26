namespace labyItems.Models.Characters;

public sealed class AbilityCountProgression
{
    public int Amount { get; set; } = 1;
    public int PerLevels { get; set; } = 1;
    public int? Minimum { get; set; }
    public int? Maximum { get; set; }

    public int ResolveCount(int achievedLevel)
    {
        var resolved = labyItems.Services.AbilityQuantityResolver.GetProgressionQuantity(achievedLevel, PerLevels, Amount);
        if (Minimum.HasValue)
            resolved = Math.Max(resolved, Minimum.Value);
        if (Maximum.HasValue)
            resolved = Math.Min(resolved, Maximum.Value);

        return Math.Max(0, resolved);
    }
}
