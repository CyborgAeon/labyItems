using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace labyItems.Services;

public sealed class MauiShareService : IShareService
{
    public Task ShareFileAsync(string title, string path)
        => Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title ?? string.Empty,
            File = new ShareFile(path)
        });
}
