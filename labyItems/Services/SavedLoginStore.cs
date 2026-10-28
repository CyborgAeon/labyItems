using labyItems.Models.Authentication;

namespace labyItems.Services;

public sealed class SavedLoginStore(ISavedLoginStorage storage, Func<Task<bool>> authenticate)
{
    public bool HasSavedLogin => storage.HasSavedLogin;
    public Task SaveAsync(string email, string password) => storage.SaveAsync(email, password);

    public async Task<SavedLogin?> UnlockAsync()
    {
        // Never retrieve saved credentials until the native biometric prompt succeeds.
        if (!HasSavedLogin || !await authenticate()) return null;
        try
        {
            var login = await storage.ReadAsync();
            if (string.IsNullOrWhiteSpace(login?.Email) || string.IsNullOrEmpty(login.Password))
            {
                Remove();
                return null;
            }
            return login;
        }
        catch
        {
            Remove();
            throw;
        }
    }

    public void Remove() => storage.Remove();
}
