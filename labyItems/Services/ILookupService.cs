namespace labyItems.Services;

public interface ILookupService
{
    Task<IReadOnlyList<LookupItem>> SearchAsync(string query);
    Task<IReadOnlyList<LookupItem>> GetAllAsync();
}

public readonly record struct LookupItem(string Key, string Display);
