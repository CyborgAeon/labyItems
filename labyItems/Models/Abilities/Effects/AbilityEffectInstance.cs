namespace labyItems.Models.Abilities.Effects;

public sealed record AbilityEffectInstance(
    string AbilityKey,
    AbilityEffectInstruction Instruction,
    int? LevelGained = null,
    int? Table = null,
    EffectCondition? Condition = null);
