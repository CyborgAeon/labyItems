using labyItems.Models.Authentication;

namespace labyItems.Services;

public interface ISavedLoginStorage
{
    bool HasSavedLogin { get; }
    Task SaveAsync(string email, string password);
    Task<SavedLogin?> ReadAsync();
    void Remove();
}
