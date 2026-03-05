using System.Threading.Tasks;

namespace labyItems.Services;

public interface IExportService
{
    Task ShareFileAsync(string title, string path);
    Task CopyTextAsync(string text);
    Task OpenFileAsync(string path);
}
