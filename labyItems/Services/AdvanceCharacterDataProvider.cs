namespace labyItems.Services;

public sealed class AdvanceCharacterDataProvider : IAdvanceCharacterDataProvider
{
    public async Task<AdvanceCharacterReferenceData> LoadReferenceDataAsync()
    {
        var dbInitializer = ServiceHelper.ResolveService<IDatabaseInitializer>();
        if (dbInitializer != null)
            await dbInitializer.InitializeAsync();

        var guildsTask = GuildsService.GetAllAsync();
        var classesTask = ClassService.GetAllAsync();
        var racesTask = PeopleService.GetAllAsync();
        var miraclesTask = MiracleService.GetAllAsync();
        var spellsTask = SpellService.GetAllAsync();
        var evocationsTask = EvocationCatalogService.GetAllAsync();

        await Task.WhenAll(
            guildsTask,
            classesTask,
            racesTask,
            miraclesTask,
            spellsTask,
            evocationsTask);

        return new AdvanceCharacterReferenceData(
            Guilds: guildsTask.Result,
            Classes: classesTask.Result,
            Races: racesTask.Result,
            Miracles: miraclesTask.Result,
            Spells: spellsTask.Result,
            Evocations: evocationsTask.Result,
            Abilities: Array.Empty<ManuAbilityService.ManuAbilityEntry>());
    }

    public async Task<IReadOnlyList<SpellService.SpellRaw>> LoadSpellsAsync()
        => await SpellService.GetAllAsync();

    public Task<IReadOnlyList<DruidEvocationService.EvocRaw>> LoadEvocationsAsync()
        => EvocationCatalogService.GetAllAsync();

    public Task<IReadOnlyList<ManuAbilityService.ManuAbilityEntry>> SearchAbilitiesAsync(string query)
        => ManuAbilityService.SearchAsync(query ?? string.Empty);
}

public sealed class AdvanceAbilityLookupService : IAdvanceAbilityLookupService
{
    public Task<EvolutionService.AbilityResult?> FindByNameAsync(string? abilityName)
        => EvolutionService.FindAbilityAsync(abilityName);
}
