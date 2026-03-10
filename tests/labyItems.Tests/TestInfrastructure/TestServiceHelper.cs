using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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

    public static async Task<string> ReadPackageTextAsync(string relativePath)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(relativePath);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
        return await reader.ReadToEndAsync();
    }

    public static SQLiteConnection OpenReadOnlyConnection()
    {
        var path = EnsureDbPath();
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"Database not found: {DbFileName}");

        return new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
    }

    public static void LogDbError(string context, Exception ex)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[TEST-DB] {context}: {ex.Message}");
        }
        catch
        {
            // ignore
        }
    }

    public static string NormalizeForNgrams(string s)
    {
        var b = new StringBuilder();
        foreach (var ch in s ?? string.Empty)
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
        var value = (s ?? string.Empty).Trim();
        if (value.Length <= n)
        {
            if (value.Length > 0)
                yield return value;
            yield break;
        }

        for (var i = 0; i <= value.Length - n; i++)
            yield return value.Substring(i, n);
    }
}
