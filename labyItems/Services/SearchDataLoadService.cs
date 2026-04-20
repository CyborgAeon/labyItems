using labyItems.Pages.Search;

namespace labyItems.Services;

public sealed class SearchDataLoadService
{
    private IReadOnlyList<GlobalSearchResultVm>? _cachedResults;
    private readonly object _cacheLock = new();

    public async Task<IReadOnlyList<GlobalSearchResultVm>> LoadAllSearchResultsAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_cacheLock)
        {
            if (_cachedResults != null)
                return _cachedResults;
        }

        var (abilities, spells, miracles, evocations) = await LoadAllDataInParallelAsync(cancellationToken);
        
        var results = await BuildResultsOnBackgroundThreadAsync(abilities, spells, miracles, evocations, cancellationToken);
        
        lock (_cacheLock)
        {
            _cachedResults = results;
        }

        return results;
    }

    private async Task<(
        IReadOnlyList<EvolutionService.AbilityResult>,
        IReadOnlyList<SpellService.SpellRaw>,
        IReadOnlyList<MiracleService.MiracRaw>,
        IReadOnlyList<DruidEvocationService.EvocRaw>)>
        LoadAllDataInParallelAsync(CancellationToken cancellationToken)
    {
        var abilityTask = SafeLoadAsync(EvolutionService.GetAllAbilitiesAsync, cancellationToken);
        var spellTask = SafeLoadAsync(() => SpellService.GetAllAsync().ContinueWith(t => (IReadOnlyList<SpellService.SpellRaw>)t.Result), cancellationToken);
        var miracleTask = SafeLoadAsync(MiracleService.GetAllAsync, cancellationToken);
        var evocationTask = SafeLoadEvocationsAsync(cancellationToken);

        await Task.WhenAll(abilityTask, spellTask, miracleTask, evocationTask);

        return (
            await abilityTask,
            await spellTask,
            await miracleTask,
            await evocationTask
        );
    }

    private async Task<IReadOnlyList<GlobalSearchResultVm>> BuildResultsOnBackgroundThreadAsync(
        IReadOnlyList<EvolutionService.AbilityResult> abilities,
        IReadOnlyList<SpellService.SpellRaw> spells,
        IReadOnlyList<MiracleService.MiracRaw> miracles,
        IReadOnlyList<DruidEvocationService.EvocRaw> evocations,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() => BuildUnifiedResults(abilities, spells, miracles, evocations), cancellationToken);
    }

    private static IReadOnlyList<GlobalSearchResultVm> BuildUnifiedResults(
        IReadOnlyList<EvolutionService.AbilityResult> abilities,
        IReadOnlyList<SpellService.SpellRaw> spells,
        IReadOnlyList<MiracleService.MiracRaw> miracles,
        IReadOnlyList<DruidEvocationService.EvocRaw> evocations)
    {
        var results = new List<GlobalSearchResultVm>();

        var abilityMap = new Dictionary<string, GlobalSearchResultVm>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in abilities)
        {
            var key = BuildAbilityKey(ability.Index, ability.Table);
            if (abilityMap.ContainsKey(key))
                continue;

            abilityMap[key] = CreateAbilityResult(ability);
        }

        results.AddRange(abilityMap.Values);

        results.AddRange(
            spells
                .Where(s => !string.IsNullOrWhiteSpace(s.name))
                .Select(CreateSpellResult));

        results.AddRange(
            miracles
                .Where(m => !string.IsNullOrWhiteSpace(m.name))
                .Select(CreateMiracleResult));

        results.AddRange(
            evocations
                .Where(e => !string.IsNullOrWhiteSpace(e.name))
                .Select(CreateEvocationResult));

        return results;
    }

    private static string BuildAbilityKey(string index, int table) => $"{table}:{index}";

    private static GlobalSearchResultVm CreateAbilityResult(EvolutionService.AbilityResult ability)
    {
        var title = (ability.Index ?? string.Empty).Trim();
        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Ability,
            Name: title,
            GroupText: $"Table {ability.Table}",
            IconGlyph: "\uf014",
            MetaText: $"Ability · Table {ability.Table} · Cost {ability.Cost}",
            DescriptionText: (ability.Description ?? string.Empty).Trim(),
            Ability: ability,
            Spell: null,
            Miracle: null,
            Evocation: null);
    }

    private static GlobalSearchResultVm CreateSpellResult(SpellService.SpellRaw spell)
    {
        var colourDisplay = (spell.colour ?? string.Empty).Trim();
        var tierDisplay = spell.isAdvanced ?? false ? "Advanced" : "Handbook";
        var metaText = $"Spell · {tierDisplay}";
        if (!string.IsNullOrEmpty(colourDisplay))
            metaText += $" · {colourDisplay}";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Spell,
            Name: (spell.name ?? string.Empty).Trim(),
            GroupText: colourDisplay,
            IconGlyph: "\uf01d",
            MetaText: metaText,
            DescriptionText: (spell.description ?? string.Empty).Trim(),
            Ability: null,
            Spell: spell,
            Miracle: null,
            Evocation: null);
    }

    private static GlobalSearchResultVm CreateMiracleResult(MiracleService.MiracRaw miracle)
    {
        var sphereDisplay = (miracle.sphere ?? string.Empty).Trim();
        var tierDisplay = miracle.isAdvanced ? "Advanced" : "Handbook";
        var metaText = $"Miracle · {tierDisplay}";
        if (!string.IsNullOrEmpty(sphereDisplay))
            metaText += $" · {sphereDisplay}";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Miracle,
            Name: (miracle.name ?? string.Empty).Trim(),
            GroupText: sphereDisplay,
            IconGlyph: "\uf204",
            MetaText: metaText,
            DescriptionText: (miracle.description ?? string.Empty).Trim(),
            Ability: null,
            Spell: null,
            Miracle: miracle,
            Evocation: null);
    }

    private static GlobalSearchResultVm CreateEvocationResult(DruidEvocationService.EvocRaw evocation)
    {
        var fieldsDisplay = string.Join(", ", evocation.fields ?? new List<string>());
        var metaText = $"Evocation · {Math.Max(0, evocation.power)} EP";
        if (!string.IsNullOrEmpty(fieldsDisplay))
            metaText += $" · {fieldsDisplay}";
        if (evocation.nonStandard)
            metaText += " · Non-standard";

        return new GlobalSearchResultVm(
            Kind: GlobalSearchKind.Evocation,
            Name: (evocation.name ?? string.Empty).Trim(),
            GroupText: fieldsDisplay,
            IconGlyph: "\uf06c",
            MetaText: metaText,
            DescriptionText: (evocation.description ?? string.Empty).Trim(),
            Ability: null,
            Spell: null,
            Miracle: null,
            Evocation: evocation);
    }

    private static async Task<IReadOnlyList<T>> SafeLoadAsync<T>(
        Func<Task<IReadOnlyList<T>>> loader,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await loader();
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError($"Failed to load {typeof(T).Name}", ex);
            return Array.Empty<T>();
        }
    }

    private static async Task<IReadOnlyList<DruidEvocationService.EvocRaw>> SafeLoadEvocationsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await DruidEvocationService.GetAllAsync();
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Failed to load evocations", ex);
            return Array.Empty<DruidEvocationService.EvocRaw>();
        }
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedResults = null;
        }
    }
}
