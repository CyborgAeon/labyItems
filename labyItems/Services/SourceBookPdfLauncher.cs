namespace labyItems.Services;

public static class SourceBookPdfLauncher
{
    public static async Task OpenAsync(string? sourceBookName, CancellationToken cancellationToken = default)
    {
        if (!SourceBookPdfCatalog.TryResolve(sourceBookName, out var assetPath))
            throw new InvalidOperationException($"No PDF is available for '{sourceBookName}'.");

        var fileName = Path.GetFileName(assetPath);
        var localPath = Path.Combine(FileSystem.CacheDirectory, fileName);

        await using (var source = await FileSystem.OpenAppPackageFileAsync(assetPath))
        await using (var destination = File.Create(localPath))
            await source.CopyToAsync(destination, cancellationToken);

        var request = new OpenFileRequest(sourceBookName?.Trim() ?? fileName, new ReadOnlyFile(localPath));
        if (!await Launcher.Default.OpenAsync(request))
            throw new InvalidOperationException("No PDF viewer is available on this device.");
    }
}
