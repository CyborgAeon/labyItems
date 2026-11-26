using labyItems.Models.Rules;

namespace labyItems.Services;

public sealed class RuleReferenceIndex
{
    private readonly Dictionary<RuleCatalogueKind, Dictionary<string, RuleDefinition>> _definitions = [];
    private readonly Dictionary<RuleCatalogueKind, Dictionary<string, HashSet<string>>> _aliases = [];

    public RuleReferenceIndex(
        IEnumerable<RuleDefinition> definitions,
        IEnumerable<(RuleCatalogueKind Catalogue, string Alias, string Target)> aliases)
    {
        foreach (var definition in definitions)
        {
            if (!Enum.IsDefined(definition.Catalogue) || string.IsNullOrWhiteSpace(definition.Identifier))
                throw new ArgumentException("Invalid rule identity.", nameof(definitions));
            if (definition.References.Any(reference => !Enum.IsDefined(reference.Catalogue)
                || !Enum.IsDefined(reference.Relationship) || string.IsNullOrWhiteSpace(reference.Identifier)))
                throw new ArgumentException($"Rule '{definition.Identifier}' has an invalid reference.", nameof(definitions));

            if (!_definitions.TryGetValue(definition.Catalogue, out var byId))
            {
                byId = new(StringComparer.OrdinalIgnoreCase);
                _definitions.Add(definition.Catalogue, byId);
            }

            byId.Add(definition.Identifier, definition with
            {
                References = definition.References.Count == 0 ? [] : Array.AsReadOnly(definition.References.ToArray())
            });
        }

        foreach (var (catalogue, alias, target) in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias) || !_definitions.TryGetValue(catalogue, out var byId)
                || !byId.TryGetValue(target, out var definition))
                throw new ArgumentException($"Invalid rule alias '{alias}' for {catalogue} '{target}'.", nameof(aliases));
            if (alias.Trim().Equals(definition.Identifier, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_aliases.TryGetValue(catalogue, out var byAlias))
            {
                byAlias = new(StringComparer.OrdinalIgnoreCase);
                _aliases.Add(catalogue, byAlias);
            }

            if (!byAlias.TryGetValue(alias.Trim(), out var targets))
            {
                targets = new(StringComparer.OrdinalIgnoreCase);
                byAlias.Add(alias.Trim(), targets);
            }

            targets.Add(definition.Identifier);
        }
    }

    public RuleDefinition? Find(RuleCatalogueKind catalogue, string identifier, out bool ambiguous)
    {
        ambiguous = false;
        var key = identifier.Trim();
        if (!_definitions.TryGetValue(catalogue, out var byId))
            return null;

        if (byId.TryGetValue(key, out var definition))
            return definition;

        if (!_aliases.TryGetValue(catalogue, out var byAlias) || !byAlias.TryGetValue(key, out var targets))
            return null;

        ambiguous = targets.Count > 1;
        return ambiguous ? null : byId[targets.Single()];
    }
}
