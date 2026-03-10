using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public static class DetailCardLookupService
{
    public static async Task<(string Key, SpecialisationRecord? Record)> FindSpecialisationAsync(string key)
    {
        var match = await FindByKeyAsync<SpecialisationRecord>(
            key,
            async () => await SpecialisationService.GetAllAsync());
        return (match.Key, match.Value);
    }

    public static async Task<(string Key, AbilityDefinition? Ability)> FindSpecialisationAbilityAsync(string key)
    {
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        var match = await FindByKeyAsync<AbilityDefinition>(
            key,
            () => Task.FromResult(index.AbilityReferences));
        return (match.Key, match.Value);
    }

    public static async Task<(string Key, TValue? Value)> FindByKeyAsync<TValue>(
        string key,
        Func<Task<IReadOnlyDictionary<string, TValue>>> loader)
        where TValue : class
    {
        var requested = (key ?? string.Empty).Trim();
        if (requested.Length == 0)
            return (string.Empty, null);

        var all = await loader();
        if (all.Count == 0)
            return (string.Empty, null);

        foreach (var entry in all)
        {
            if (!entry.Key.Equals(requested, StringComparison.OrdinalIgnoreCase))
                continue;

            return (entry.Key, entry.Value);
        }

        return (string.Empty, null);
    }
}
