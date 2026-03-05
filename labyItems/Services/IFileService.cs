using System.Threading.Tasks;

namespace labyItems.Services;

public interface IFileService
{
    string CacheDirectory { get; }
    string AppDataDirectory { get; }
    string CombineCachePath(string fileName);
    Task<string> ReadPackageTextAsync(string relativePath);
    Task WriteTextAsync(string path, string content);
}
