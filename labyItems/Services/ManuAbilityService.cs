using labyItems.Models.Rules;

namespace labyItems.Services;

public static class ManuAbilityService
{
    private const string ManufacturerSourceBook = "Manufacturers Guide";

    public record ManuAbilityEntry(
        string name,
        string abilityRef,
        string availability,
        IReadOnlyList<RuleClause> availabilityRules,
        int cost,
        int table,
        string description,
        bool canBuyMultiple,
        IReadOnlyList<string> preReqs,
        IReadOnlyList<string> choiceSetRefs,
        string sourceBook);

    private static List<ManuAbilityEntry>? _manufacturingCache;
    private static List<ManuAbilityEntry>? _mergedCatalogCache;

    public static async Task<IReadOnlyList<ManuAbilityEntry>> GetAllAsync()
    {
        if (_manufacturingCache is { Count: > 0 }) return _manufacturingCache;
        try
        {
            var list = await GetMergedCatalogAsync();
            _manufacturingCache = list
                .Where(IsManufacturerAbility)
                .OrderBy(e => e.name)
                .ToList();
        }
        catch (Exception ex)
        {
            LogError("GetAll abilities", ex);
            return _manufacturingCache ?? new List<ManuAbilityEntry>();
        }

        return _manufacturingCache;
    }

    public static async Task<IReadOnlyList<ManuAbilityEntry>> SearchAsync(string query)
    {
        List<ManuAbilityEntry> mapped;
        try
        {
            var results = await SearchMergedCatalogAsync(query);
            mapped = results
                .Where(IsManufacturerAbility)
                .OrderBy(e => e.name)
                .ToList();
        }
        catch (Exception ex)
        {
            LogError("Search abilities", ex);
            mapped = new List<ManuAbilityEntry>();
        }

        return mapped;
    }

    public static async Task<IReadOnlyList<ManuAbilityEntry>> GetMergedCatalogAsync()
    {
        if (_mergedCatalogCache is { Count: > 0 })
            return _mergedCatalogCache;

        try
        {
            var list = await EvolutionService.GetAllAbilitiesAsync();
            _mergedCatalogCache = list
                .Select(MapEntry)
                .OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.sourceBook, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            LogError("Get merged ability catalog", ex);
            return _mergedCatalogCache ?? new List<ManuAbilityEntry>();
        }

        return _mergedCatalogCache;
    }

    public static async Task<IReadOnlyList<ManuAbilityEntry>> SearchMergedCatalogAsync(string query)
    {
        try
        {
            var results = string.IsNullOrWhiteSpace(query)
                ? await EvolutionService.GetAllAbilitiesAsync()
                : await EvolutionService.SearchAbilitiesAsync(query);

            return results
                .Select(MapEntry)
                .OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.sourceBook, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            LogError("Search merged ability catalog", ex);
            return new List<ManuAbilityEntry>();
        }
    }

    public static void InvalidateCache()
    {
        _manufacturingCache = null;
        _mergedCatalogCache = null;
    }

    private static void LogError(string context, Exception ex)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[Abilities] {context}: {ex}");
            Console.WriteLine($"[Abilities] {context}: {ex}");
        }
        catch
        {
            // ignore logging failures
        }
    }

    private static ManuAbilityEntry MapEntry(EvolutionService.AbilityResult ability)
        => new(
            ability.Index,
            ability.AbilityRef,
            ability.Available,
            ability.AvailabilityRules,
            ability.Cost,
            ability.Table,
            ability.Description,
            ability.CanBuyMultiple,
            ability.PreReqs,
            ability.ChoiceSetRefs,
            ability.SourceBook);

    private static bool IsManufacturerAbility(EvolutionService.AbilityResult ability)
    {
        var sourceBook = (ability.SourceBook ?? string.Empty).Trim();
        if (sourceBook.Equals(ManufacturerSourceBook, StringComparison.OrdinalIgnoreCase))
            return true;

        var abilityRef = (ability.AbilityRef ?? string.Empty).Trim();
        if (abilityRef.StartsWith("ability.make.", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsManufacturerAbility(ManuAbilityEntry ability)
    {
        var sourceBook = (ability.sourceBook ?? string.Empty).Trim();
        if (sourceBook.Equals(ManufacturerSourceBook, StringComparison.OrdinalIgnoreCase))
            return true;

        var abilityRef = (ability.abilityRef ?? string.Empty).Trim();
        if (abilityRef.StartsWith("ability.make.", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
