using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public sealed class MauiFileService : IFileService
{
    public string CacheDirectory => FileSystem.CacheDirectory;
    public string AppDataDirectory => FileSystem.AppDataDirectory;

    public string CombineCachePath(string fileName)
        => Path.Combine(CacheDirectory, fileName ?? string.Empty);

    public async Task<string> ReadPackageTextAsync(string relativePath)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(relativePath);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public Task WriteTextAsync(string path, string content)
        => File.WriteAllTextAsync(path, content ?? string.Empty);
}
