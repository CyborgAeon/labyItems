namespace labyItems.Services.Helpers;

/// <summary>
/// Generic indexed cache structure for fast O(1) exact lookups and efficient contains/prefix searching
/// </summary>
public class IndexedCache<T>
{
    /// <summary>
    /// Dictionary for O(1) exact match lookups using normalized keys
    /// </summary>
    public required Dictionary<string, T> ExactMatches { get; init; }

    /// <summary>
    /// Sorted list of normalized keys with items for efficient contains/prefix searching
    /// </summary>
    public required List<IndexedItem> Items { get; init; }

    public record IndexedItem(string NormalizedKey, T Item);

    /// <summary>
    /// Find items by normalized key containing the query string
    /// </summary>
    public List<T> SearchByContains(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Items.Select(x => x.Item).ToList();

        var normalized = NormalizeKey(query);
        return Items
            .Where(x => x.NormalizedKey.Contains(normalized))
            .Select(x => x.Item)
            .ToList();
    }

    /// <summary>
    /// Find exact match by normalized key (O(1))
    /// </summary>
    public T? TryGetExact(string key)
    {
        var normalized = NormalizeKey(key);
        return ExactMatches.TryGetValue(normalized, out var item) ? item : default;
    }

    /// <summary>
    /// Normalize key for consistent searching: trim and lowercase
    /// </summary>
    private static string NormalizeKey(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();
}
