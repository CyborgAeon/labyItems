using Android.App;
using Android.Net;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

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

        var uriString = (reference.PersistentReference ?? reference.SourceUri ?? string.Empty).Trim();
        if (uriString.Length == 0)
            return null;

        var uri = Android.Net.Uri.Parse(uriString);
        return ImageSource.FromStream(() =>
        {
            var activity = Platform.CurrentActivity ?? Android.App.Application.Context as Activity;
            var resolver = activity?.ContentResolver ?? Android.App.Application.Context.ContentResolver;
            return resolver?.OpenInputStream(uri) ?? Stream.Null;
        });
    }
}
