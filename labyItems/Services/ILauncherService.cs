using System.Threading.Tasks;

namespace labyItems.Services;

public interface ILauncherService
{
    Task OpenFileAsync(string path);
}
