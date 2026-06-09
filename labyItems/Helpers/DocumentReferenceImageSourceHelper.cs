using labyItems.Services;

namespace labyItems.Helpers;

public static partial class DocumentReferenceImageSourceHelper
{
    public static ImageSource? CreateImageSource(DocumentReferenceInfo? reference)
    {
        if (reference == null || !reference.HasReference || !LooksLikeImage(reference))
            return null;

#if ANDROID || IOS
        return CreatePlatformImageSource(reference);
#else
        return CreateLocalPathImageSource(reference);
#endif
    }

    internal static ImageSource? CreateLocalPathImageSource(DocumentReferenceInfo reference)
    {
        var path = ResolveLocalPathReference(reference);
        return path.Length > 0 ? ImageSource.FromFile(path) : null;
    }

    internal static string ResolveLocalPathReference(DocumentReferenceInfo reference)
    {
        var persistent = (reference.PersistentReference ?? string.Empty).Trim();
        if (persistent.Length > 0
            && (string.Equals(reference.StorageKind, DocumentReferenceKinds.LocalPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(reference.StorageKind, DocumentReferenceKinds.LegacyPath, StringComparison.OrdinalIgnoreCase)))
        {
            return persistent;
        }

        return (reference.DisplayPath ?? reference.SourceUri ?? string.Empty).Trim();
    }

    private static bool LooksLikeImage(DocumentReferenceInfo reference)
    {
        var contentType = (reference.ContentType ?? string.Empty).Trim();
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return true;

        var path = (reference.FileName ?? reference.DisplayPath ?? reference.SourceUri ?? reference.PersistentReference ?? string.Empty).Trim();
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp";
    }

#if ANDROID || IOS
    private static partial ImageSource? CreatePlatformImageSource(DocumentReferenceInfo reference);
#endif
}
