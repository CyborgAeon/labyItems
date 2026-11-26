using labyItems.Models.Rules;

namespace labyItems.Services;

public sealed class RuleReferenceResolver(RuleReferenceIndex index)
{
    public RuleResolution Resolve(RuleCatalogueKind catalogue, string identifier, bool includeInformational = true,
        IReadOnlyList<RuleReference>? effectiveReferences = null)
    {
        var diagnostics = new List<RuleResolutionDiagnostic>();
        var root = Find(catalogue, identifier, null);
        if (root is null)
            return new(null, [], [], [], diagnostics.AsReadOnly());

        var visited = new HashSet<RuleDefinition>(ReferenceEqualityComparer.Instance) { root };
        var related = new List<RuleDefinition>();
        var links = new List<ResolvedRuleLink>();
        var pending = new Queue<RuleDefinition>();
        var mechanicalLinks = new Dictionary<RuleDefinition, List<RuleDefinition>>(ReferenceEqualityComparer.Instance);
        var incomplete = new HashSet<RuleDefinition>(ReferenceEqualityComparer.Instance);
        pending.Enqueue(root);

        while (pending.TryDequeue(out var rule))
        {
            var references = ReferenceEquals(rule, root) && effectiveReferences != null ? effectiveReferences : rule.References;
            var uniqueTargets = new HashSet<(RuleCatalogueKind Catalogue, string Identifier, RuleRelationship Relationship)>();
            foreach (var reference in references)
            {
                var mechanical = reference.Relationship is RuleRelationship.Inherits or RuleRelationship.Modifies;
                if (!mechanical && !includeInformational)
                    continue;

                var target = Find(reference.Catalogue, reference.Identifier, rule.Identifier);
                if (target is null)
                {
                    if (mechanical)
                        incomplete.Add(rule);
                    continue;
                }

                if (uniqueTargets.Add((target.Catalogue, target.Identifier, reference.Relationship)))
                {
                    links.Add(new(rule, reference.Relationship, target));
                    if (mechanical)
                    {
                        if (!mechanicalLinks.TryGetValue(rule, out var dependencies))
                        {
                            dependencies = [];
                            mechanicalLinks.Add(rule, dependencies);
                        }

                        dependencies.Add(target);
                    }
                }

                if (!visited.Add(target))
                    continue;

                related.Add(target);
                pending.Enqueue(target);
            }
        }

        var resolved = new HashSet<RuleDefinition>(ReferenceEqualityComparer.Instance);
        var active = new HashSet<RuleDefinition>(ReferenceEqualityComparer.Instance);
        var mechanicalOrder = new List<RuleDefinition>();
        var validMechanicalOrder = VisitMechanical(root, mechanicalOrder);
        foreach (var rule in related)
            VisitMechanical(rule, null);

        return new(root, related.AsReadOnly(), links.AsReadOnly(),
            validMechanicalOrder ? mechanicalOrder.AsReadOnly() : [], diagnostics.Distinct().ToArray());

        RuleDefinition? Find(RuleCatalogueKind kind, string id, string? referencedBy)
        {
            var result = index.Find(kind, id, out var ambiguous);
            if (result is null)
            {
                diagnostics.Add(new(
                    ambiguous ? RuleResolutionDiagnosticKind.AmbiguousReference : RuleResolutionDiagnosticKind.MissingReference,
                    kind, id, referencedBy));
            }

            return result;
        }

        bool VisitMechanical(RuleDefinition rule, List<RuleDefinition>? order)
        {
            if (resolved.Contains(rule))
                return !incomplete.Contains(rule);

            if (!active.Add(rule))
            {
                diagnostics.Add(new(RuleResolutionDiagnosticKind.CircularInheritance, rule.Catalogue, rule.Identifier, null));
                return false;
            }

            if (mechanicalLinks.TryGetValue(rule, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    if (!VisitMechanical(dependency, order))
                        incomplete.Add(rule);
                }
            }

            active.Remove(rule);
            resolved.Add(rule);
            order?.Add(rule);
            return !incomplete.Contains(rule);
        }
    }
}
