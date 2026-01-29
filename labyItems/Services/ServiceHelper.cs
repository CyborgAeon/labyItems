using System.Text;
using Microsoft.Maui.Storage;
using SQLite;

namespace labyItems.Services;

public static class ServiceHelper
{
    public const string DbFileName = "laby.db";
    private static string? _dbPath;

    public static string? EnsureDbPath()
    {
        if (!string.IsNullOrWhiteSpace(_dbPath) && File.Exists(_dbPath))
            return _dbPath;

        var appDb = Path.Combine(FileSystem.AppDataDirectory, DbFileName);
        if (File.Exists(appDb))
        {
            _dbPath = appDb;
            return _dbPath;
        }

        var devDb = Path.Combine(Directory.GetCurrentDirectory(), "output", DbFileName);
        if (File.Exists(devDb))
        {
            _dbPath = devDb;
            return _dbPath;
        }

        _dbPath = null;
        return null;
    }

    public static SQLiteConnection OpenReadOnlyConnection()
    {
        var path = EnsureDbPath();
        if (string.IsNullOrEmpty(path))
        {
            var ex = new InvalidOperationException($"Database not found; ensure {DbFileName} is present in app data or available during development.");
            LogDbError("Open database", ex);
            throw ex;
        }

        return new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
    }

    public static void LogDbError(string context, Exception ex)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[DB] {context}: {ex}");
            Console.WriteLine($"[DB] {context}: {ex}");
        }
        catch
        {
            // ignore logging failures
        }
    }

    public static string NormalizeForNgrams(string s)
    {
        var b = new StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                b.Append(ch);
            else
                b.Append(' ');
        }

        return string.Join(' ', b.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    public static IEnumerable<string> GenerateNGrams(string s, int n)
    {
        var t = s.Replace(" ", " ");
        if (t.Length <= n)
        {
            yield return t;
            yield break;
        }

        for (var i = 0; i <= t.Length - n; i++)
            yield return t.Substring(i, n);
    }
}
