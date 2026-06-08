using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public interface ICacheMaintenanceService
{
    Task RunStartupCleanupAsync(CancellationToken cancellationToken = default);
}

public sealed class CacheMaintenanceService : ICacheMaintenanceService
{
    private static readonly TimeSpan StaleCacheAge = TimeSpan.FromDays(2);
    private const long TargetCacheBytes = 50L * 1024L * 1024L;
    private const long TrimToBytes = 25L * 1024L * 1024L;

    private readonly ILogger<CacheMaintenanceService> _logger;

    public CacheMaintenanceService(ILogger<CacheMaintenanceService> logger)
    {
        _logger = logger;
    }

    public Task RunStartupCleanupAsync(CancellationToken cancellationToken = default)
        => Task.Run(() => RunStartupCleanup(cancellationToken), cancellationToken);

    private void RunStartupCleanup(CancellationToken cancellationToken)
    {
        var cacheDirectory = FileSystem.CacheDirectory;
        if (string.IsNullOrWhiteSpace(cacheDirectory) || !Directory.Exists(cacheDirectory))
            return;

        var files = SafeEnumerateFiles(cacheDirectory).ToList();
        var now = DateTimeOffset.UtcNow;

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            TryDeleteIfStaleOrTemporary(file, now);
        }

        TrimCacheDirectory(cacheDirectory, cancellationToken);
    }

    private void TryDeleteIfStaleOrTemporary(FileInfo file, DateTimeOffset now)
    {
        try
        {
            if (!file.Exists)
                return;

            var fileName = file.Name;
            var age = now - file.LastWriteTimeUtc;
            var isTemporaryPackageCopy = fileName.StartsWith("laby.pkg.", StringComparison.OrdinalIgnoreCase)
                                         && fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase);
            var isGeneratedExport = IsGeneratedExport(fileName);

            if (isTemporaryPackageCopy || (isGeneratedExport && age >= StaleCacheAge))
                file.Delete();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to delete cache file {Path}", file.FullName);
        }
    }

    private void TrimCacheDirectory(string cacheDirectory, CancellationToken cancellationToken)
    {
        var files = SafeEnumerateFiles(cacheDirectory)
            .Where(file => file.Exists)
            .OrderBy(file => file.LastWriteTimeUtc)
            .ToList();

        var totalBytes = files.Sum(file => SafeLength(file));
        if (totalBytes <= TargetCacheBytes)
            return;

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested || totalBytes <= TrimToBytes)
                return;

            try
            {
                var length = SafeLength(file);
                file.Delete();
                totalBytes -= length;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to trim cache file {Path}", file.FullName);
            }
        }
    }

    private static bool IsGeneratedExport(string fileName)
    {
        if (fileName.StartsWith("Battleboard_", StringComparison.OrdinalIgnoreCase))
            return true;
        if (fileName.StartsWith("Spells_", StringComparison.OrdinalIgnoreCase))
            return true;
        if (fileName.StartsWith("Miracles_", StringComparison.OrdinalIgnoreCase))
            return true;
        if (fileName.StartsWith("creation-preview-", StringComparison.OrdinalIgnoreCase))
            return true;

        var extension = Path.GetExtension(fileName);
        return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<FileInfo> SafeEnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .ToList();
        }
        catch
        {
            return Array.Empty<FileInfo>();
        }
    }

    private static long SafeLength(FileInfo file)
    {
        try
        {
            return file.Exists ? file.Length : 0;
        }
        catch
        {
            return 0;
        }
    }
}
