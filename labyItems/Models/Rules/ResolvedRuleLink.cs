namespace labyItems.Models.Rules;

public sealed record ResolvedRuleLink(RuleDefinition From, RuleRelationship Relationship, RuleDefinition To);
