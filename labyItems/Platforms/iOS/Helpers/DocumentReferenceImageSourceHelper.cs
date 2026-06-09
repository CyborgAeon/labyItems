using Foundation;
using labyItems.Services;
using Photos;

namespace labyItems.Helpers;

public static partial class DocumentReferenceImageSourceHelper
{
    private static partial ImageSource? CreatePlatformImageSource(DocumentReferenceInfo reference)
    {
        if (string.Equals(reference.StorageKind, DocumentReferenceKinds.LocalPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(reference.StorageKind, DocumentReferenceKinds.LegacyPath, StringComparison.OrdinalIgnoreCase))
        {
            return CreateLocalPathImageSource(reference);
        }

        return ImageSource.FromStream(() => OpenPlatformStream(reference));
    }

    private static Stream OpenPlatformStream(DocumentReferenceInfo reference)
    {
        if (string.Equals(reference.AccessReference, "photo-library-asset", StringComparison.OrdinalIgnoreCase))
            return OpenPhotoAssetStream(reference);

        var url = ResolveSecurityScopedUrl(reference, out _);
        if (url == null)
            return Stream.Null;

        var started = url.StartAccessingSecurityScopedResource();
        try
        {
            var path = (url.Path ?? string.Empty).Trim();
            return path.Length > 0 && File.Exists(path)
                ? new MemoryStream(File.ReadAllBytes(path))
                : Stream.Null;
        }
        finally
        {
            if (started)
                url.StopAccessingSecurityScopedResource();
        }
    }

    private static Stream OpenPhotoAssetStream(DocumentReferenceInfo reference)
    {
        var localId = (reference.PersistentReference ?? string.Empty).Trim();
        if (localId.Length == 0)
            return Stream.Null;

        var assetResults = PHAsset.FetchAssetsUsingLocalIdentifiers(new[] { localId }, null);
        var asset = assetResults.Count > 0 ? assetResults[0] as PHAsset : null;
        if (asset == null)
            return Stream.Null;

        var completion = new TaskCompletionSource<NSData?>();
        PHImageManager.DefaultManager.RequestImageDataAndOrientation(asset, null, (data, _, _, _) =>
        {
            completion.TrySetResult(data);
        });

        var imageData = completion.Task.GetAwaiter().GetResult();
        return imageData == null
            ? Stream.Null
            : new MemoryStream(imageData.ToArray());
    }

    private static NSUrl? ResolveSecurityScopedUrl(DocumentReferenceInfo reference, out bool staleBookmark)
    {
        staleBookmark = false;
        var bookmarkValue = (reference.PersistentReference ?? string.Empty).Trim();
        if (bookmarkValue.Length == 0)
            return null;

        if (!string.Equals(reference.StorageKind, DocumentReferenceKinds.IosSecurityScopedBookmark, StringComparison.OrdinalIgnoreCase))
            return NSUrl.FromString(bookmarkValue);

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
}
