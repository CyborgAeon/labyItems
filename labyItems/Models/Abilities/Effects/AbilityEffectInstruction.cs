namespace labyItems.Models.Abilities.Effects;

public enum AbilityEffectInstructionKind
{
    ResistanceLevelOverride = 1,
    Immunity = 2,
    ResistanceLevelMultiplier = 3,
    ResistanceInfinite = 4,
    Unknown = 99
}

public sealed record AbilityEffectInstruction(
    AbilityEffectInstructionKind Kind,
    // For `ResistanceLevelOverride`
    // For `ResistanceLevelMultiplier`
    // For `ResistanceInfinite`
    string? ResistanceType,
    // For `ResistanceLevelOverride`: absolute level
    // For `ResistanceLevelMultiplier`: multiplier (e.g. 2 for half-effect)
    int? Level,
    // For `Immunity`
    string? ImmunityName);
