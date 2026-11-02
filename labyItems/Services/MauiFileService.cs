using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public sealed class MauiFileService : IFileService
{
    public string CacheDirectory => PrivateDirectory("exports");
    public string AppDataDirectory => PrivateDirectory("documents");

    private static string PrivateDirectory(string name)
    {
        var root = AccountDataAccess.Workspace?.RequireDirectory() ?? FileSystem.AppDataDirectory;
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public string CombineCachePath(string fileName)
        => Path.Combine(CacheDirectory, fileName ?? string.Empty);

    public async Task<string> ReadPackageTextAsync(string relativePath)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(relativePath);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public async Task WriteTextAsync(string path, string content)
    {
        var version = LabyrintheSession.Shared.Version;
        await File.WriteAllTextAsync(path, content ?? string.Empty);
        LabyrintheSession.Shared.RequireVersion(version);
    }
}
