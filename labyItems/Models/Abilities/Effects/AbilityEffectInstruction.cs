namespace labyItems.Models.Abilities.Effects;

public enum AbilityEffectInstructionKind
{
    ResistanceLevelOverride = 1,
    Immunity = 2,
    Unknown = 99
}

public sealed record AbilityEffectInstruction(
    AbilityEffectInstructionKind Kind,
    // For `ResistanceLevelOverride`
    string? ResistanceType,
    int? Level,
    // For `Immunity`
    string? ImmunityName);

