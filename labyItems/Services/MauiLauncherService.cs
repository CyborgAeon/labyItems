using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Services;

public sealed class MauiLauncherService : ILauncherService
{
    public Task OpenFileAsync(string path)
        => Launcher.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(path)
        });
}
