using labyItems.Models.Characters;
using labyItems.Services.Guilds;

namespace labyItems.Services;

public sealed class AdvanceCharacterDataProvider : IAdvanceCharacterDataProvider
{
    public async Task<AdvanceCharacterReferenceData> LoadReferenceDataAsync()
    {
        var dbInitializer = ServiceHelper.ResolveService<IDatabaseInitializer>();
        if (dbInitializer != null)
            await dbInitializer.InitializeAsync();

        var guildMiracleListsTask = GuildMiracleListCatalogue.GetAllAsync();
        var guildsTask = GuildsService.GetMiracleSearchAsync();
        var classesTask = ClassService.GetAllAsync();
        var racesTask = PeopleService.GetAllAsync();
        var miraclesTask = MiracleService.GetAllAsync();
        var spellsTask = SpellService.GetAllAsync();
        var evocationsTask = EvocationCatalogService.GetAllAsync();

        await Task.WhenAll(
            guildMiracleListsTask,
            guildsTask,
            classesTask,
            racesTask,
            miraclesTask,
            spellsTask,
            evocationsTask);

        return new AdvanceCharacterReferenceData(
            GuildMiracleLists: guildMiracleListsTask.Result,
            Guilds: guildsTask.Result,
            Classes: classesTask.Result,
            Races: racesTask.Result,
            Miracles: miraclesTask.Result,
            Spells: spellsTask.Result,
            Evocations: evocationsTask.Result,
            Abilities: Array.Empty<EvolutionService.AbilityResult>());
    }

    public async Task<IReadOnlyList<SpellService.SpellRaw>> LoadSpellsAsync()
        => await SpellService.GetAllAsync();

    public Task<IReadOnlyList<DruidEvocationService.EvocRaw>> LoadEvocationsAsync()
        => EvocationCatalogService.GetAllAsync();

    public Task<IReadOnlyList<EvolutionService.AbilityResult>> SearchAbilitiesAsync(string query)
        => EvolutionService.SearchAbilitiesAsync(query ?? string.Empty);
}

public sealed class AdvanceAbilityLookupService : IAdvanceAbilityLookupService
{
    public Task<EvolutionService.AbilityResult?> FindByNameAsync(string? abilityName)
        => EvolutionService.FindAbilityAsync(abilityName);
}
