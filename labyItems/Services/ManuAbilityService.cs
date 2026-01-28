namespace labyItems.Services;

public static class ManuAbilityService
{
    public record ManuAbilityEntry(
        string name,
        string availability,
        int cost,
        int table,
        string description,
        bool canBuyMultiple,
        IReadOnlyList<string> preReqs);

    private static List<ManuAbilityEntry>? _cache;

    public static async Task<IReadOnlyList<ManuAbilityEntry>> GetAllAsync()
    {
        if (_cache is { Count: > 0 }) return _cache;
        try
        {
            var list = await GeneralService.GetAllAbilitiesAsync();
            _cache = list
                .Select(e => new ManuAbilityEntry(
                    e.Index,
                    e.Available,
                    e.Cost,
                    e.Table,
                    e.Description,
                    e.CanBuyMultiple,
                    e.PreReqs))
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
            var results = await GeneralService.SearchAbilitiesAsync(query);
            mapped = results
                .Select(e => new ManuAbilityEntry(
                    e.Index,
                    e.Available,
                    e.Cost,
                    e.Table,
                    e.Description,
                    e.CanBuyMultiple,
                    e.PreReqs))
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
}
