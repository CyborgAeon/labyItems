using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

internal static class MultiPathCatalogueAbilityResolver
{
    public static async Task BindAsync(IEnumerable<MultiPathLevelAbility> abilities)
    {
        var index = await SpecialisationDefinitionRepository.GetIndexAsync().ConfigureAwait(false);
        foreach (var ability in abilities)
        {
            if (index.AbilityReferences.TryGetValue(ability.AbilityRef, out var canonical)
                && string.Equals(ability.AbilityRef, canonical.Key, StringComparison.Ordinal))
            {
                ability.BindCanonicalText(canonical);
            }
        }
    }
}
