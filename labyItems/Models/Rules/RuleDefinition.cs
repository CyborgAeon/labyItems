namespace labyItems.Models.Rules;

public sealed record RuleDefinition(
    RuleCatalogueKind Catalogue,
    string Identifier,
    string Name,
    string? Text,
    string? Source,
    IReadOnlyList<RuleReference> References,
    string? Family = null);
