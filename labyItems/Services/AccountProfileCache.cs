using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using labyItems.Models.Authentication;

namespace labyItems.Services;

/// <summary>Verified profiles for multiple accounts, backed by platform secure storage.</summary>
public sealed class AccountProfileCache(Func<Task<string?>> read, Func<string, Task> write) : IAccountProfileCache
{
    private readonly SemaphoreSlim _writes = new(1, 1);
    public async Task<AccountProfile?> GetAsync(string email, string password)
    {
        try
        {
            var json = await read();
            var profiles = ReadProfiles(json);
            var fingerprint = CredentialKey(email, password);
            var cached = profiles.TryGetValue(email.Trim().ToLowerInvariant(), out var current)
                ? current : profiles.GetValueOrDefault(fingerprint);
            return cached?.CredentialKey == fingerprint ? cached.Profile : null;
        }
        catch (Exception)
        {
            // Unavailable/corrupt device storage should not prevent a normal online login.
            return null;
        }
    }

    public async Task SaveAsync(string email, string password, AccountProfile profile)
    {
        await _writes.WaitAsync();
        try
        {
            var profiles = ReadProfiles(await read());
            profiles[email.Trim().ToLowerInvariant()] = new CachedProfile(CredentialKey(email, password), profile);
            await write(JsonSerializer.Serialize(profiles));
        }
        catch (Exception) { /* Keep the authenticated online session if device storage is unavailable. */ }
        finally { _writes.Release(); }
    }

    private static Dictionary<string, CachedProfile> ReadProfiles(string? json)
    {
        if (json == null) return new();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("CredentialKey", out _))
        {
            var legacy = JsonSerializer.Deserialize<CachedProfile>(json);
            return legacy == null ? new() : new() { [legacy.CredentialKey] = legacy };
        }
        return JsonSerializer.Deserialize<Dictionary<string, CachedProfile>>(json) ?? new();
    }

    private static string CredentialKey(string email, string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{email.Trim().ToLowerInvariant()}\0{password}")));

    // The fingerprint is stored inside secure storage, never in plain preferences.
    private sealed record CachedProfile(string CredentialKey, AccountProfile Profile);
}
