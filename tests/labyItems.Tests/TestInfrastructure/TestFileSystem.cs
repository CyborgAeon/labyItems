using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Maui.Storage;

public static class FileSystem
{
    private static readonly Dictionary<string, Func<Stream>> PackageOverrides = new(StringComparer.OrdinalIgnoreCase);

    public static string AppDataDirectory { get; set; } = Path.GetTempPath();

    public static string CacheDirectory { get; set; } = Path.GetTempPath();

    public static string PackageRootDirectory { get; set; } = Directory.GetCurrentDirectory();

    public static void Configure(string appDataDirectory, string cacheDirectory, string packageRootDirectory)
    {
        AppDataDirectory = appDataDirectory;
        CacheDirectory = cacheDirectory;
        PackageRootDirectory = packageRootDirectory;
    }

    public static void SetPackageOverride(string relativePath, Func<Stream> streamFactory)
    {
        if (streamFactory == null)
            throw new ArgumentNullException(nameof(streamFactory));

        PackageOverrides[NormalizePath(relativePath)] = streamFactory;
    }

    public static void ClearPackageOverrides() => PackageOverrides.Clear();

    public static Task<Stream> OpenAppPackageFileAsync(string relativePath)
    {
        var key = NormalizePath(relativePath);
        if (PackageOverrides.TryGetValue(key, out var streamFactory))
            return Task.FromResult(streamFactory());

        var candidate = Path.Combine(PackageRootDirectory, key.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(candidate)
            && key.Equals("grimoire/spells.json", StringComparison.OrdinalIgnoreCase))
        {
            candidate = Path.Combine(PackageRootDirectory, "grimoire", "allSpells.json");
        }

        if (!File.Exists(candidate))
            throw new FileNotFoundException($"Package asset not found: {relativePath}", candidate);

        return Task.FromResult<Stream>(File.OpenRead(candidate));
    }

    private static string NormalizePath(string relativePath)
        => (relativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
}
