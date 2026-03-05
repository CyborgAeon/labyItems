using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace labyItems.Services;

public sealed class MauiClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
        => Clipboard.Default.SetTextAsync(text ?? string.Empty);
}
