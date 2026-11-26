namespace labyItems.Models.Rules;

public sealed record RuleReference(RuleCatalogueKind Catalogue, string Identifier, RuleRelationship Relationship);
