using labyItems.Models.Characters;

namespace labyItems.Services;

public static class AbilityUsageResolver
{
    public static IReadOnlyList<ResolvedAbilityUsage> ResolveIntervals(
        IEnumerable<ResolvedAbilityUsage> activePolicies,
        IEnumerable<InnateAbilityDraft>? innates)
    {
        var innateTotals = (innates ?? Enumerable.Empty<InnateAbilityDraft>())
            .Where(innate => innate.Rank > 0)
            .ToLookup(innate => innate.Name, innate => innate.Rank, StringComparer.OrdinalIgnoreCase);

        return activePolicies
            .Where(usage => usage.Policy.Uses > 0 && usage.Policy.Minutes > 0)
            .GroupBy(usage => (usage.Source, usage.AbilityRef))
            .Select(policies => policies.Last())
            .Where(usage => usage.Policy.RequiredInnates == 0
                            || innateTotals[usage.Name].Sum(rank => (long)rank) >= usage.Policy.RequiredInnates)
            .ToArray();
    }
}
