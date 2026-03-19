namespace labyItems.Models.Abilities.Effects;

public sealed class AbilityEffectEvaluationResult
{
    public Dictionary<string, int> ResistanceOverrides { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<string> Immunities { get; } = new();

    // Generic numeric deltas for progressive rollout (e.g. "TBLP", "LOC", "PAC", "DAC", ...).
    public Dictionary<string, int> StatDeltas { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Constraints/overrides for non-additive effects (e.g. set loc to half tblp).
    public Dictionary<string, string> Constraints { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
