using System.Threading.Tasks;

namespace labyItems.Services;

public interface IShareService
{
    Task ShareFileAsync(string title, string path);
}
