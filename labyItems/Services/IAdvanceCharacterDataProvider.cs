using labyItems.Models.Characters;

namespace labyItems.Services;

public interface IAdvanceCharacterDataProvider
{
    Task<AdvanceCharacterReferenceData> LoadReferenceDataAsync();
    Task<IReadOnlyList<SpellService.SpellRaw>> LoadSpellsAsync();
    Task<IReadOnlyList<DruidEvocationService.EvocRaw>> LoadEvocationsAsync();
    Task<IReadOnlyList<EvolutionService.AbilityResult>> SearchAbilitiesAsync(string query);
}

public interface IAdvanceAbilityLookupService
{
    Task<EvolutionService.AbilityResult?> FindByNameAsync(string? abilityName);
}
