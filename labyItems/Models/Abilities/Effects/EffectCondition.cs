namespace labyItems.Models.Abilities.Effects;

public sealed record EffectCondition(
    string? Field = null,
    string? Operator = null,
    IReadOnlyList<string>? Values = null)
{
    public static EffectCondition Always { get; } = new();

    public IReadOnlyList<string> NormalizedValues => Values ?? Array.Empty<string>();
}
