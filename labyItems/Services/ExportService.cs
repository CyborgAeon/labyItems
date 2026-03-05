using System;
using System.Threading.Tasks;

namespace labyItems.Services;

public sealed class ExportService : IExportService
{
    private readonly IClipboardService _clipboardService;
    private readonly ILauncherService _launcherService;
    private readonly IShareService _shareService;

    public ExportService(
        IClipboardService clipboardService,
        ILauncherService launcherService,
        IShareService shareService)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _launcherService = launcherService ?? throw new ArgumentNullException(nameof(launcherService));
        _shareService = shareService ?? throw new ArgumentNullException(nameof(shareService));
    }

    public Task ShareFileAsync(string title, string path)
        => _shareService.ShareFileAsync(title, path);

    public Task CopyTextAsync(string text)
        => _clipboardService.SetTextAsync(text);

    public Task OpenFileAsync(string path)
        => _launcherService.OpenFileAsync(path);
}
