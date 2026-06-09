using Foundation;
using Photos;
using UIKit;
using UniformTypeIdentifiers;

namespace labyItems.Services;

public sealed partial class DocumentReferenceService
{
    private partial Task<DocumentReferenceCapture?> PickDocumentPlatformAsync()
    {
        var presenter = GetPresenter();
        var tcs = new TaskCompletionSource<DocumentReferenceCapture?>();

        var supportedTypes = new[]
        {
            UTType.CreateFromIdentifier("public.image"),
            UTType.CreateFromIdentifier("com.adobe.pdf")
        }
        .Where(type => type != null)
        .Cast<UTType>()
        .ToArray();

        if (supportedTypes.Length == 0)
            throw new InvalidOperationException("Unable to configure supported document types for iOS document picking.");

        var picker = new UIDocumentPickerViewController(
            supportedTypes,
            asCopy: false)
        {
            AllowsMultipleSelection = false,
            ModalPresentationStyle = UIModalPresentationStyle.FormSheet
        };

        var pickerDelegate = new BookmarkDocumentPickerDelegate(tcs);
        picker.Delegate = pickerDelegate;
        picker.WasCancelled += HandleCancelled;
        presenter.PresentViewController(picker, true, null);
        return tcs.Task;

        void HandleCancelled(object? sender, EventArgs args)
        {
            picker.WasCancelled -= HandleCancelled;
            tcs.TrySetResult(null);
        }
    }

    private partial Task<DocumentReferenceCapture?> PickImagePlatformAsync()
    {
        var presenter = GetPresenter();
        var tcs = new TaskCompletionSource<DocumentReferenceCapture?>();

        var supportedTypes = new[]
        {
            UTType.CreateFromIdentifier("public.image")
        }
        .Where(type => type != null)
        .Cast<UTType>()
        .ToArray();

        if (supportedTypes.Length == 0)
            throw new InvalidOperationException("Unable to configure supported image types for iOS image picking.");

        var picker = new UIDocumentPickerViewController(
            supportedTypes,
            asCopy: false)
        {
            AllowsMultipleSelection = false,
            ModalPresentationStyle = UIModalPresentationStyle.FormSheet
        };

        var pickerDelegate = new BookmarkDocumentPickerDelegate(tcs);
        picker.Delegate = pickerDelegate;
        picker.WasCancelled += HandleCancelled;
        presenter.PresentViewController(picker, true, null);
        return tcs.Task;

        void HandleCancelled(object? sender, EventArgs args)
        {
            picker.WasCancelled -= HandleCancelled;
            tcs.TrySetResult(null);
        }
    }

    private partial async Task<DocumentReferenceCapture?> CapturePhotoPlatformAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
            throw new InvalidOperationException("This device does not support taking photos from the app.");

        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
            throw new InvalidOperationException("Allow camera access to attach a photo to this creation.");

        var photoPermission = await Permissions.RequestAsync<Permissions.Photos>();
        if (photoPermission != PermissionStatus.Granted)
            throw new InvalidOperationException("Allow photo library access so the captured image can be linked long term.");

        var file = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
        {
            Title = $"Creation photo {DateTime.Now:yyyyMMdd_HHmmss}"
        });

        if (file == null)
            return null;

        var fullPath = (file.FullPath ?? string.Empty).Trim();
        if (fullPath.Length == 0)
            return CreatePathCapture(file);

        var assetId = await SaveCapturedPhotoToLibraryAsync(fullPath);
        return new DocumentReferenceCapture(
            StorageKind: DocumentReferenceKinds.IosSecurityScopedBookmark,
            PersistentReference: assetId,
            SourceUri: fullPath,
            AccessReference: "photo-library-asset",
            DisplayPath: file.FileName,
            FileName: file.FileName,
            ContentType: file.ContentType ?? "image/jpeg");
    }

    private partial Task<bool> IsAvailablePlatformAsync(NonStandardDocumentLink document)
    {
        if (string.Equals(document.AccessReference, "photo-library-asset", StringComparison.OrdinalIgnoreCase))
        {
            var localId = (document.PersistentReference ?? string.Empty).Trim();
            if (localId.Length == 0)
                return Task.FromResult(false);

            var fetch = PHAsset.FetchAssetsUsingLocalIdentifiers(new[] { localId }, null);
            return Task.FromResult(fetch.Count > 0);
        }

        try
        {
            return Task.FromResult(ResolveSecurityScopedUrl(document, out _) != null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private partial async Task OpenPlatformAsync(NonStandardDocumentLink document)
    {
        if (string.Equals(document.AccessReference, "photo-library-asset", StringComparison.OrdinalIgnoreCase))
        {
            var localId = (document.PersistentReference ?? string.Empty).Trim();
            if (localId.Length == 0)
                throw new InvalidOperationException("This photo reference is missing its asset identifier.");

            var assetResults = PHAsset.FetchAssetsUsingLocalIdentifiers(new[] { localId }, null);
            var asset = assetResults.Count > 0 ? assetResults[0] as PHAsset : null;
            if (asset == null)
                throw new FileNotFoundException("That linked photo is no longer available on this device.");

            var tempPath = Path.Combine(FileSystem.CacheDirectory, $"creation-preview-{document.Id}.jpg");
            await ExportPhotoAssetAsync(asset, tempPath);
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(tempPath)
            });
            return;
        }

        var url = ResolveSecurityScopedUrl(document, out var staleBookmark)
            ?? throw new FileNotFoundException("That linked file is no longer available on this device.");

        var started = url.StartAccessingSecurityScopedResource();
        try
        {
            if (staleBookmark)
            {
                // The resolved URL is still usable, so open it even if iOS reports the bookmark as stale.
            }

            await Launcher.Default.OpenAsync(new Uri(url.AbsoluteString ?? url.Path ?? string.Empty));
        }
        finally
        {
            if (started)
                url.StopAccessingSecurityScopedResource();
        }
    }

    private static UIViewController GetPresenter()
    {
        var window = UIApplication.SharedApplication
            .ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow)
            ?? UIApplication.SharedApplication.Windows.FirstOrDefault(window => window.IsKeyWindow);

        var controller = window?.RootViewController
            ?? throw new InvalidOperationException("The iOS presenter is unavailable.");

        while (controller.PresentedViewController != null)
            controller = controller.PresentedViewController;

        return controller;
    }

    private static NSUrl? ResolveSecurityScopedUrl(NonStandardDocumentLink document, out bool staleBookmark)
    {
        staleBookmark = false;
        var bookmarkValue = (document.PersistentReference ?? string.Empty).Trim();
        if (bookmarkValue.Length == 0)
            return null;

        if (string.Equals(document.StorageKind, DocumentReferenceKinds.LocalPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(document.StorageKind, DocumentReferenceKinds.LegacyPath, StringComparison.OrdinalIgnoreCase))
        {
            return NSUrl.FromFilename(bookmarkValue);
        }

        if (!string.Equals(document.StorageKind, DocumentReferenceKinds.IosSecurityScopedBookmark, StringComparison.OrdinalIgnoreCase)
            || string.Equals(document.AccessReference, "photo-library-asset", StringComparison.OrdinalIgnoreCase))
        {
            return NSUrl.FromString(bookmarkValue);
        }

        try
        {
            var bookmarkData = NSData.FromArray(Convert.FromBase64String(bookmarkValue));
            var url = NSUrl.FromBookmarkData(
                bookmarkData,
                NSUrlBookmarkResolutionOptions.WithSecurityScope,
                null,
                out staleBookmark,
                out var error);

            if (error != null)
                throw new InvalidOperationException(error.LocalizedDescription);

            return url;
        }
        catch
        {
            return null;
        }
    }

    private static Task<string> SaveCapturedPhotoToLibraryAsync(string fullPath)
    {
        var tcs = new TaskCompletionSource<string>();
        PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
        {
            var creationRequest = PHAssetCreationRequest.CreationRequestForAsset();
            creationRequest.AddResource(PHAssetResourceType.Photo, NSUrl.FromFilename(fullPath), null);
            var placeholder = creationRequest.PlaceholderForCreatedAsset;
            if (placeholder != null)
                tcs.TrySetResult(placeholder.LocalIdentifier);
        },
        (success, error) =>
        {
            if (!success || error != null)
            {
                tcs.TrySetException(new InvalidOperationException(error?.LocalizedDescription ?? "Unable to save the captured photo."));
                return;
            }

            if (tcs.Task.IsCompleted)
                return;

            tcs.TrySetException(new InvalidOperationException("The captured photo did not return a persistent photo-library reference."));
        });

        return tcs.Task;
    }

    private static Task ExportPhotoAssetAsync(PHAsset asset, string targetPath)
    {
        var tcs = new TaskCompletionSource();
        PHImageManager.DefaultManager.RequestImageDataAndOrientation(asset, null, async (data, _, _, info) =>
        {
            try
            {
                if (data == null)
                    throw new InvalidOperationException("The linked photo data is unavailable.");

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? FileSystem.CacheDirectory);
                await File.WriteAllBytesAsync(targetPath, data.ToArray());
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private sealed class BookmarkDocumentPickerDelegate(TaskCompletionSource<DocumentReferenceCapture?> completionSource) : UIDocumentPickerDelegate
    {
        public override void WasCancelled(UIDocumentPickerViewController controller)
            => completionSource.TrySetResult(null);

        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            var url = urls?.FirstOrDefault();
            if (url == null)
            {
                completionSource.TrySetResult(null);
                return;
            }

            var started = url.StartAccessingSecurityScopedResource();
            try
            {
                var bookmark = url.CreateBookmarkData(
                    NSUrlBookmarkCreationOptions.WithSecurityScope,
                    Array.Empty<string>(),
                    null,
                    out var error);

                if (error != null)
                    throw new InvalidOperationException(error.LocalizedDescription);

                completionSource.TrySetResult(new DocumentReferenceCapture(
                    StorageKind: DocumentReferenceKinds.IosSecurityScopedBookmark,
                    PersistentReference: Convert.ToBase64String(bookmark.ToArray()),
                    SourceUri: url.AbsoluteString,
                    AccessReference: "security-scoped",
                    DisplayPath: url.Path,
                    FileName: url.LastPathComponent,
                    ContentType: ResolveContentType(url)));
            }
            catch (Exception ex)
            {
                completionSource.TrySetException(ex);
            }
            finally
            {
                if (started)
                    url.StopAccessingSecurityScopedResource();
            }
        }

        private static string? ResolveContentType(NSUrl url)
        {
            var extension = Path.GetExtension(url.Path ?? string.Empty).TrimStart('.').ToLowerInvariant();
            if (extension.Length == 0)
                return null;

            var uti = UTType.CreateFromExtension(extension);
            return uti?.PreferredMimeType;
        }
    }
}
