using labyItems.Models.Characters;

namespace labyItems.Services;

public sealed record AdvanceCharacterReferenceData(
    IReadOnlyDictionary<string, GuildRecord> Guilds,
    IReadOnlyDictionary<string, CharacterClassRecord> Classes,
    IReadOnlyDictionary<string, PeopleRecord> Races,
    IReadOnlyList<MiracleService.MiracRaw> Miracles,
    IReadOnlyList<SpellService.SpellRaw> Spells,
    IReadOnlyList<DruidEvocationService.EvocRaw> Evocations,
    IReadOnlyList<ManuAbilityService.ManuAbilityEntry> Abilities);

public interface IAdvanceCharacterDataProvider
{
    Task<AdvanceCharacterReferenceData> LoadReferenceDataAsync();
    Task<IReadOnlyList<SpellService.SpellRaw>> LoadSpellsAsync();
    Task<IReadOnlyList<DruidEvocationService.EvocRaw>> LoadEvocationsAsync();
    Task<IReadOnlyList<ManuAbilityService.ManuAbilityEntry>> SearchAbilitiesAsync(string query);
}

public interface IAdvanceAbilityLookupService
{
    Task<EvolutionService.AbilityResult?> FindByNameAsync(string? abilityName);
}
