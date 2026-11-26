namespace labyItems.Models.Rules;

public sealed record RuleResolution(
    RuleDefinition? Root,
    IReadOnlyList<RuleDefinition> RelatedRules,
    IReadOnlyList<ResolvedRuleLink> Links,
    IReadOnlyList<RuleDefinition> MechanicalOrder,
    IReadOnlyList<RuleResolutionDiagnostic> Diagnostics);
