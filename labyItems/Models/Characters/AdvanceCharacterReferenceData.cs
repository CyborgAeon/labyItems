using labyItems.Models.Guilds;
using labyItems.Services;
using CharacterClassRecord = labyItems.Services.CharacterClassRecord;

namespace labyItems.Models.Characters;

public sealed record AdvanceCharacterReferenceData(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> GuildMiracleLists,
    IReadOnlyDictionary<string, GuildRecord> Guilds,
    IReadOnlyDictionary<string, CharacterClassRecord> Classes,
    IReadOnlyDictionary<string, PeopleRecord> Races,
    IReadOnlyList<MiracleService.MiracRaw> Miracles,
    IReadOnlyList<SpellService.SpellRaw> Spells,
    IReadOnlyList<DruidEvocationService.EvocRaw> Evocations,
    IReadOnlyList<EvolutionService.AbilityResult> Abilities);

