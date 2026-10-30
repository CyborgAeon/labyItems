using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using labyItems.Models.Calendar;

namespace labyItems.Services;

public sealed class FileCalendarEventCache(string directory) : ICalendarEventCache
{
    private string GetPath(string account) => Path.Combine(directory,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account.Trim().ToLowerInvariant()))) + ".json");

    public async Task<CalendarCacheSnapshot?> LoadAsync(string account)
    {
        try
        {
            var path = GetPath(account);
            return File.Exists(path) ? JsonSerializer.Deserialize<CalendarCacheSnapshot>(await File.ReadAllTextAsync(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(string account, CalendarCacheSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = GetPath(account);
            await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(snapshot));
            File.Move(path + ".tmp", path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Disk availability must not interrupt a successful online refresh.
        }
    }
}
