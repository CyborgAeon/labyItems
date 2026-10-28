using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using labyItems.Models.Authentication;

namespace labyItems.Services;

/// <summary>A single account snapshot, backed by the platform's secure storage.</summary>
public sealed class AccountProfileCache(Func<Task<string?>> read, Func<string, Task> write) : IAccountProfileCache
{
    public async Task<AccountProfile?> GetAsync(string email, string password)
    {
        try
        {
            var json = await read();
            var cached = json == null ? null : JsonSerializer.Deserialize<CachedProfile>(json);
            return cached?.CredentialKey == CredentialKey(email, password) ? cached.Profile : null;
        }
        catch (Exception)
        {
            // Unavailable/corrupt device storage should not prevent a normal online login.
            return null;
        }
    }

    public async Task SaveAsync(string email, string password, AccountProfile profile)
    {
        try { await write(JsonSerializer.Serialize(new CachedProfile(CredentialKey(email, password), profile))); }
        catch (Exception) { /* Keep the authenticated online session if device storage is unavailable. */ }
    }

    private static string CredentialKey(string email, string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{email.Trim().ToLowerInvariant()}\0{password}")));

    // The fingerprint is stored inside secure storage, never in plain preferences.
    private sealed record CachedProfile(string CredentialKey, AccountProfile Profile);
}
