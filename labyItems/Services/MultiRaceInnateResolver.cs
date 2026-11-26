using System.Globalization;
using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public static class MultiRaceInnateResolver
{
    public static async Task<IReadOnlyList<InnateAbilityDraft>> ResolveAsync(CharacterDraft draft)
    {
        if (draft.MultiRaceLevel <= 0 || string.IsNullOrWhiteSpace(draft.MultiRaceKey))
            return [];

        var session = LabyrintheSession.Shared;
        var version = session.Version;
        var catalog = await MultiRaceService.GetCatalogAsync().ConfigureAwait(false);
        session.RequireVersion(version);
        if (!catalog.MultiRaces.TryGetValue(draft.MultiRaceKey, out var definition))
            return [];

        var level = Math.Min(draft.MultiRaceLevel, definition.MaxLevel > 0 ? definition.MaxLevel : 6);
        var active = Enumerable.Range(1, level).SelectMany(acquiredAt =>
            definition.Levels.TryGetValue(acquiredAt.ToString(CultureInfo.InvariantCulture), out var grants)
                ? grants.Where(grant => grant.PreReqs is not { Count: > 0 })
                : []);
        var result = new List<InnateAbilityDraft>();
        var eligibleGrants = active.ToArray();
        var racialProjection = eligibleGrants.LastOrDefault(grant => grant.RacialInnateLevel.HasValue);
        foreach (var grant in eligibleGrants)
        {
            if (grant.Progression != null && grant.Type.Equals("Innate", StringComparison.OrdinalIgnoreCase))
            {
                Add(result, grant.Name, grant.Progression.ResolveCount(level));
            }

            if (grant != racialProjection || grant.RacialInnateLevel is not int racialLevel || grant.ChoiceSetRefs == null)
                continue;

            var index = await SpecialisationDefinitionRepository.GetIndexAsync().ConfigureAwait(false);
            session.RequireVersion(version);
            foreach (var reference in grant.ChoiceSetRefs)
            {
                if (draft.MultiRaceChoiceSelections?.TryGetValue(reference, out var key) != true
                    || !index.ChoiceSetTemplates.TryGetValue(reference, out var choices))
                    continue;

                var option = choices.Options.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
                if (option != null)
                    result.AddRange(ResolveRacialInnates(option, racialLevel > 0 ? racialLevel : level));
            }
        }

        return result;
    }

    public static IReadOnlyList<InnateAbilityDraft> ResolveRacialInnates(ChoiceOption option, int level)
    {
        var result = new List<InnateAbilityDraft>();
        foreach (var grant in option.Grants)
        {
            if (!grant.Ability.Type.Equals("Innate", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var ability in AbilityDraftBuilder.ParseAbility(grant.Ability, grant.Level, level))
                Add(result, ability.Name, AbilityDraftBuilder.ResolveInnateRank(ability));
        }

        return result;
    }

    private static void Add(List<InnateAbilityDraft> result, string name, int rank)
    {
        if (rank > 0 && !string.IsNullOrWhiteSpace(name))
            result.Add(new InnateAbilityDraft { Name = name, Rank = rank });
    }
}
