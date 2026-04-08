using labyItems.Models.Abilities;

namespace labyItems.Services;

public sealed record AbilityPrerequisiteIssue(
    string AbilityName,
    IReadOnlyList<string> MissingPrerequisites,
    IReadOnlyList<string> MissingPrerequisiteKeys);

public sealed record AbilityPrerequisiteCheckResult(
    IReadOnlyList<AbilityPrerequisiteIssue> Issues,
    IReadOnlyList<string> MissingPrerequisiteKeys)
{
    public bool HasIssues => Issues.Count > 0;
}

public static class AbilityPrerequisiteService
{
    public static AbilityPrerequisiteCheckResult Evaluate(
        IEnumerable<EvolutionService.AbilityResult>? selectedAbilities,
        IEnumerable<string>? knownAbilityTerms,
        IEnumerable<EvolutionService.AbilityResult>? abilityCatalog)
    {
        var selected = (selectedAbilities ?? Array.Empty<EvolutionService.AbilityResult>())
            .Where(ability => ability != null)
            .ToList();
        if (selected.Count == 0)
            return new AbilityPrerequisiteCheckResult(Array.Empty<AbilityPrerequisiteIssue>(), Array.Empty<string>());

        var allAbilities = (abilityCatalog ?? selected)
            .Where(ability => ability != null)
            .ToList();

        var aliasLookup = BuildAliasLookup(allAbilities);
        var ownershipProfile = AbilityOwnershipService.Build(
            knownAbilityTerms,
            selected,
            aliasLookup);

        var issues = new List<AbilityPrerequisiteIssue>();
        var missingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ability in selected)
        {
            var missingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missingAbilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var preReq in ability.PreReqs ?? Array.Empty<string>())
            {
                if (!TryParseAbilityPreReq(preReq, out var requiredTerm))
                    continue;

                if (IsSatisfied(requiredTerm, aliasLookup, ownershipProfile))
                    continue;

                var resolved = ResolveAbility(requiredTerm, aliasLookup);
                var displayName = (resolved?.Index ?? requiredTerm).Trim();
                if (displayName.Length > 0)
                    missingNames.Add(displayName);

                var key = ResolveCanonicalKey(requiredTerm, resolved);
                if (key.Length > 0)
                {
                    missingAbilityKeys.Add(key);
                    missingKeys.Add(key);
                }
            }

            if (missingNames.Count == 0)
                continue;

            var abilityName = (ability.Index ?? string.Empty).Trim();
            if (abilityName.Length == 0)
                abilityName = "Ability";

            issues.Add(new AbilityPrerequisiteIssue(
                abilityName,
                missingNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList(),
                missingAbilityKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList()));
        }

        return new AbilityPrerequisiteCheckResult(
            issues.OrderBy(issue => issue.AbilityName, StringComparer.OrdinalIgnoreCase).ToList(),
            missingKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static bool IsSatisfied(
        string requiredTerm,
        IReadOnlyDictionary<string, EvolutionService.AbilityResult> aliasLookup,
        AbilityOwnershipProfile ownershipProfile)
    {
        if (AbilityOwnershipService.TryParseStrengthRequirement(requiredTerm, out var requiredStrength))
            return ownershipProfile.StrengthGrade >= requiredStrength;

        if (AbilityOwnershipService.TryParseWeaponMasteryRequirement(requiredTerm, out var requiredMastery))
            return ownershipProfile.WeaponMasteryGrade >= requiredMastery;

        foreach (var equivalent in EvolutionService.GetEquivalentAbilityNames(requiredTerm))
        {
            var normalized = AbilityDetailsLookupService.NormalizeKey(equivalent);
            if (normalized.Length > 0 && ownershipProfile.Aliases.Contains(normalized))
                return true;

            if (aliasLookup.TryGetValue(normalized, out var resolved))
            {
                if (HasAnyAlias(resolved, ownershipProfile.Aliases))
                    return true;
            }
        }

        return false;
    }

    private static bool HasAnyAlias(EvolutionService.AbilityResult ability, IReadOnlySet<string> aliases)
    {
        foreach (var candidate in GetEquivalentTerms(ability))
        {
            var normalized = AbilityDetailsLookupService.NormalizeKey(candidate);
            if (normalized.Length > 0 && aliases.Contains(normalized))
                return true;
        }

        return false;
    }

    private static string ResolveCanonicalKey(string requiredTerm, EvolutionService.AbilityResult? ability)
    {
        if (ability != null)
        {
            var key = AbilityKey.Build(ability);
            if (key.Length > 0)
                return key;
        }

        return requiredTerm.Trim();
    }

    private static EvolutionService.AbilityResult? ResolveAbility(
        string rawTerm,
        IReadOnlyDictionary<string, EvolutionService.AbilityResult> aliasLookup)
    {
        foreach (var equivalent in EvolutionService.GetEquivalentAbilityNames(rawTerm))
        {
            var normalized = AbilityDetailsLookupService.NormalizeKey(equivalent);
            if (normalized.Length == 0)
                continue;

            if (aliasLookup.TryGetValue(normalized, out var resolved))
                return resolved;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, EvolutionService.AbilityResult> BuildAliasLookup(
        IEnumerable<EvolutionService.AbilityResult> abilities)
    {
        var lookup = new Dictionary<string, EvolutionService.AbilityResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in abilities)
        {
            foreach (var candidate in GetEquivalentTerms(ability))
            {
                var normalized = AbilityDetailsLookupService.NormalizeKey(candidate);
                if (normalized.Length == 0 || lookup.ContainsKey(normalized))
                    continue;

                lookup[normalized] = ability;
            }
        }

        return lookup;
    }

    private static IEnumerable<string> GetEquivalentTerms(EvolutionService.AbilityResult ability)
    {
        foreach (var equivalent in EvolutionService.GetEquivalentAbilityNames(ability.Index))
            yield return equivalent;

        var abilityRef = (ability.AbilityRef ?? string.Empty).Trim();
        if (abilityRef.Length > 0)
            yield return abilityRef;

        var abilityKey = AbilityKey.Build(ability);
        if (abilityKey.Length > 0)
            yield return abilityKey;

        var legacyKey = AbilityKey.BuildEvolutionFallback(ability);
        if (legacyKey.Length > 0)
            yield return legacyKey;
    }

    private static bool TryParseAbilityPreReq(string? raw, out string requiredTerm)
    {
        requiredTerm = string.Empty;
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var parts = text.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            if (!parts[0].Equals("Ability", StringComparison.OrdinalIgnoreCase))
                return false;

            requiredTerm = parts[1];
            return requiredTerm.Length > 0;
        }

        requiredTerm = text;
        return true;
    }
}
