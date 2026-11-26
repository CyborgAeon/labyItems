using System.Globalization;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class MultiClassUsageService
{
    public static async Task<IReadOnlyList<ResolvedAbilityUsage>> ResolveAsync(CharacterDraft draft)
    {
        if (draft.MultiClassLevels is not { Count: > 0 })
            return [];

        var session = LabyrintheSession.Shared;
        var version = session.Version;
        var catalogue = await MultiClassService.GetCatalogAsync().ConfigureAwait(false);
        session.RequireVersion(version);
        return AbilityUsageResolver.ResolveIntervals(
            ReadActivePolicies(catalogue, draft.MultiClassLevels),
            draft.Innates);
    }

    private static IEnumerable<ResolvedAbilityUsage> ReadActivePolicies(
        MultiClassCatalog catalogue,
        IReadOnlyDictionary<string, int> selections)
    {
        foreach (var selection in selections)
        {
            if (!catalogue.MultiClasses.TryGetValue(selection.Key, out var definition))
                continue;

            var levelCap = definition.MaxLevel > 0 ? definition.MaxLevel : 6;
            var achievedLevel = Math.Min(levelCap, selection.Value);
            for (var level = 1; level <= achievedLevel; level++)
            {
                if (!definition.Levels.TryGetValue(level.ToString(CultureInfo.InvariantCulture), out var abilities))
                    continue;

                foreach (var ability in abilities)
                {
                    if (ability.Usage is { } policy)
                        yield return new(selection.Key, ability.AbilityRef, ability.Name, ability.Effect, policy);
                }
            }
        }
    }
}
