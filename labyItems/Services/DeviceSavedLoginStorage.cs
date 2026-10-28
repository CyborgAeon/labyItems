using System.Text.Json;
using labyItems.Models.Authentication;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public sealed class DeviceSavedLoginStorage : ISavedLoginStorage
{
    private const string CredentialsKey = "labyrinthe.saved-login.v1";
    private const string SavedFlag = "labyrinthe.has-saved-login.v1";
    public bool HasSavedLogin => Preferences.Default.Get(SavedFlag, false);

    public async Task SaveAsync(string email, string password)
    {
        await SecureStorage.Default.SetAsync(CredentialsKey, JsonSerializer.Serialize(new SavedLogin(email, password)));
        Preferences.Default.Set(SavedFlag, true);
    }

    public async Task<SavedLogin?> ReadAsync()
    {
        var json = await SecureStorage.Default.GetAsync(CredentialsKey);
        return json == null ? null : JsonSerializer.Deserialize<SavedLogin>(json);
    }

    public void Remove()
    {
        SecureStorage.Default.Remove(CredentialsKey);
        Preferences.Default.Remove(SavedFlag);
    }
}
