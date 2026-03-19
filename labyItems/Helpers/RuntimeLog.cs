using System.Text;
using Microsoft.Maui.Storage;

namespace labyItems.Helpers;

public static class RuntimeLog
{
    private static readonly object Sync = new();
    private static string? _logPath;

    public static string LogPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_logPath))
                return _logPath;

            _logPath = Path.Combine(FileSystem.AppDataDirectory, "runtime.log");
            return _logPath;
        }
    }

    public static void Write(string category, string message, Exception? ex = null)
    {
        var safeCategory = (category ?? "APP").Trim();
        if (safeCategory.Length == 0)
            safeCategory = "APP";

        var safeMessage = (message ?? string.Empty).Trim();
        var builder = new StringBuilder();
        builder.Append('[')
            .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'"))
            .Append("] [")
            .Append(safeCategory)
            .Append("] ")
            .Append(safeMessage);

        if (ex != null)
        {
            builder.AppendLine();
            builder.Append(ex);
        }

        var line = builder.ToString();
        try
        {
            System.Diagnostics.Debug.WriteLine(line);
            Console.WriteLine(line);
        }
        catch
        {
            // Ignore logging transport issues.
        }

        try
        {
            lock (Sync)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore file I/O logging failures.
        }
    }
}
