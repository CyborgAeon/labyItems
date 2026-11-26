namespace labyItems.Models.Rules;

public sealed record RuleResolutionDiagnostic(
    RuleResolutionDiagnosticKind Kind,
    RuleCatalogueKind Catalogue,
    string Identifier,
    string? ReferencedBy);
