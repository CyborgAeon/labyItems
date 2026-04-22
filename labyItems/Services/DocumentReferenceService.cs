using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public sealed partial class DocumentReferenceService : IDocumentReferenceService
{
    private static readonly FilePickerFileType AllowedDocumentTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.iOS] = ["public.image", "com.adobe.pdf"],
        [DevicePlatform.MacCatalyst] = ["public.image", "com.adobe.pdf"],
        [DevicePlatform.Android] = ["image/*", "application/pdf"],
        [DevicePlatform.WinUI] = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".pdf"],
        [DevicePlatform.Tizen] = ["image/*", "application/pdf"]
    });

    public async Task<DocumentReferenceCapture?> PickDocumentAsync()
    {
#if ANDROID || IOS
        return await PickDocumentPlatformAsync();
#else
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select an image or PDF",
            FileTypes = AllowedDocumentTypes
        });

        return file == null ? null : CreatePathCapture(file);
#endif
    }

    public async Task<DocumentReferenceCapture?> CapturePhotoAsync()
    {
#if ANDROID || IOS
        return await CapturePhotoPlatformAsync();
#else
        var file = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
        {
            Title = $"Creation photo {DateTime.Now:yyyyMMdd_HHmmss}"
        });

        return file == null ? null : CreatePathCapture(file);
#endif
    }

    public Task<bool> IsAvailableAsync(NonStandardDocumentLink document)
    {
#if ANDROID || IOS
        return IsAvailablePlatformAsync(document);
#else
        var reference = ResolvePathReference(document);
        return Task.FromResult(reference.Length > 0 && File.Exists(reference));
#endif
    }

    public async Task OpenAsync(NonStandardDocumentLink document)
    {
#if ANDROID || IOS
        await OpenPlatformAsync(document);
#else
        var reference = ResolvePathReference(document);
        if (reference.Length == 0 || !File.Exists(reference))
            throw new FileNotFoundException("That file is no longer available on this device.", reference);

        await Launcher.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(reference)
        });
#endif
    }

#if ANDROID || IOS
    private partial Task<DocumentReferenceCapture?> PickDocumentPlatformAsync();
    private partial Task<DocumentReferenceCapture?> CapturePhotoPlatformAsync();
    private partial Task<bool> IsAvailablePlatformAsync(NonStandardDocumentLink document);
    private partial Task OpenPlatformAsync(NonStandardDocumentLink document);
#endif

    internal static string ResolvePathReference(NonStandardDocumentLink document)
        => (document.PersistentReference ?? string.Empty).Trim().Length > 0
            && (document.StorageKind == DocumentReferenceKinds.LocalPath || document.StorageKind == DocumentReferenceKinds.LegacyPath)
                ? document.PersistentReference.Trim()
                : (document.FilePath ?? string.Empty).Trim();

    internal static DocumentReferenceCapture CreatePathCapture(FileResult file)
    {
        var persistedPath = (file.FullPath ?? file.FileName ?? string.Empty).Trim();
        return new DocumentReferenceCapture(
            StorageKind: DocumentReferenceKinds.LocalPath,
            PersistentReference: persistedPath,
            SourceUri: persistedPath,
            AccessReference: null,
            DisplayPath: persistedPath,
            FileName: file.FileName,
            ContentType: file.ContentType);
    }
}
