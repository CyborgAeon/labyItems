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
        IReadOnlyList<string> choiceSetRefs);

    private static List<ManuAbilityEntry>? _cache;

    public static async Task<IReadOnlyList<ManuAbilityEntry>> GetAllAsync()
    {
        if (_cache is { Count: > 0 }) return _cache;
        try
        {
            var list = await EvolutionService.GetAllAbilitiesAsync();
            _cache = list
                .Where(IsManufacturerAbility)
                .Select(MapEntry)
                .OrderBy(e => e.name)
                .ToList();
        }
        catch (Exception ex)
        {
            LogError("GetAll abilities", ex);
            return _cache ?? new List<ManuAbilityEntry>();
        }

        return _cache;
    }

    public static async Task<IReadOnlyList<ManuAbilityEntry>> SearchAsync(string query)
    {
        List<ManuAbilityEntry> mapped;
        try
        {
            var results = await EvolutionService.SearchAbilitiesAsync(query);
            mapped = results
                .Where(IsManufacturerAbility)
                .Select(MapEntry)
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
            ability.ChoiceSetRefs);

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
}
