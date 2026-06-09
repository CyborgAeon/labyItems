using Android.App;
using Android.Content;
using Android.Database;
using Android.Net;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Services;

public sealed partial class DocumentReferenceService
{
    private static int _requestCodeSeed = 7100;

    private partial Task<DocumentReferenceCapture?> PickDocumentPlatformAsync()
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("Android activity is unavailable.");
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            intent.PutExtra(Intent.ExtraMimeTypes, new[] { "image/*", "application/pdf" });
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);

            var result = await StartIntentAsync(activity, intent);
            if (result.ResultCode != Result.Ok || result.Data?.Data is not { } uri)
                return null;

            PersistUriPermission(activity, result.Data, uri);
            return BuildCapture(activity, uri, DocumentReferenceKinds.AndroidDocumentUri, "persisted-read");
        });

    private partial Task<DocumentReferenceCapture?> PickImagePlatformAsync()
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("Android activity is unavailable.");
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("image/*");
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);

            var result = await StartIntentAsync(activity, intent);
            if (result.ResultCode != Result.Ok || result.Data?.Data is not { } uri)
                return null;

            PersistUriPermission(activity, result.Data, uri);
            return BuildCapture(activity, uri, DocumentReferenceKinds.AndroidDocumentUri, "persisted-read");
        });

    private partial Task<DocumentReferenceCapture?> CapturePhotoPlatformAsync()
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var permission = await Permissions.RequestAsync<Permissions.Camera>();
            if (permission != PermissionStatus.Granted)
                throw new InvalidOperationException("Allow camera access to attach a photo to this creation.");

            var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("Android activity is unavailable.");
            var resolver = activity.ContentResolver ?? throw new InvalidOperationException("Android content resolver is unavailable.");

            var values = new ContentValues();
            var fileName = $"creation_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(MediaStore.IMediaColumns.MimeType, "image/jpeg");
            values.Put(MediaStore.IMediaColumns.RelativePath, "Pictures/labyItems");

            var uri = resolver.Insert(MediaStore.Images.Media.ExternalContentUri, values);
            if (uri == null)
                throw new InvalidOperationException("Unable to prepare a photo destination.");

            var intent = new Intent(MediaStore.ActionImageCapture);
            intent.PutExtra(MediaStore.ExtraOutput, uri);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            intent.ClipData = ClipData.NewRawUri(fileName, uri);

            var result = await StartIntentAsync(activity, intent);
            if (result.ResultCode != Result.Ok)
            {
                resolver.Delete(uri, null, null);
                return null;
            }

            return BuildCapture(activity, uri, DocumentReferenceKinds.AndroidMediaUri, "app-owned");
        });

    private partial Task<bool> IsAvailablePlatformAsync(NonStandardDocumentLink document)
        => MainThread.InvokeOnMainThreadAsync(() =>
        {
            var activity = Platform.CurrentActivity ?? Android.App.Application.Context as Activity;
            var resolver = activity?.ContentResolver ?? Android.App.Application.Context.ContentResolver;
            var uriString = ResolveAndroidUri(document);
            if (resolver == null || uriString.Length == 0 || Android.Net.Uri.Parse(uriString) is not { } uri)
                return false;

            try
            {
                using var stream = resolver.OpenInputStream(uri);
                return stream != null;
            }
            catch
            {
                return false;
            }
        });

    private partial Task OpenPlatformAsync(NonStandardDocumentLink document)
        => MainThread.InvokeOnMainThreadAsync(() =>
        {
            var uriString = ResolveAndroidUri(document);
            if (uriString.Length == 0)
                throw new InvalidOperationException("This file reference is missing its Android document URI.");

            var uri = Android.Net.Uri.Parse(uriString);
            var contentType = string.IsNullOrWhiteSpace(document.ContentType) ? "*/*" : document.ContentType;
            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(uri, contentType);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
        });

    private static DocumentReferenceCapture BuildCapture(Context context, Android.Net.Uri uri, string storageKind, string accessReference)
    {
        var resolver = context.ContentResolver;
        var displayName = TryGetDisplayName(resolver, uri);
        var contentType = resolver?.GetType(uri);

        return new DocumentReferenceCapture(
            StorageKind: storageKind,
            PersistentReference: uri.ToString() ?? string.Empty,
            SourceUri: uri.ToString(),
            AccessReference: accessReference,
            DisplayPath: displayName ?? uri.LastPathSegment,
            FileName: displayName ?? uri.LastPathSegment,
            ContentType: contentType);
    }

    private static string ResolveAndroidUri(NonStandardDocumentLink document)
    {
        if (!string.IsNullOrWhiteSpace(document.PersistentReference))
            return document.PersistentReference.Trim();

        return (document.SourceUri ?? document.FilePath ?? string.Empty).Trim();
    }

    private static void PersistUriPermission(Activity activity, Intent? data, Android.Net.Uri uri)
    {
        try
        {
            var takeFlags = data?.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission)
                ?? ActivityFlags.GrantReadUriPermission;
            if (takeFlags == 0)
                takeFlags = ActivityFlags.GrantReadUriPermission;

            activity.ContentResolver?.TakePersistableUriPermission(uri, takeFlags);
        }
        catch
        {
            // Some providers do not expose persistable permissions even through OPEN_DOCUMENT.
        }
    }

    private static string? TryGetDisplayName(ContentResolver? resolver, Android.Net.Uri uri)
    {
        if (resolver == null)
            return null;

        try
        {
            using ICursor? cursor = resolver.Query(uri, new[] { OpenableColumns.DisplayName }, null, null, null);
            if (cursor == null || !cursor.MoveToFirst())
                return null;

            var index = cursor.GetColumnIndex(OpenableColumns.DisplayName);
            return index >= 0 ? cursor.GetString(index) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Task<(Result ResultCode, Intent? Data)> StartIntentAsync(Activity activity, Intent intent)
    {
        var requestCode = Interlocked.Increment(ref _requestCodeSeed);
        var tcs = new TaskCompletionSource<(Result ResultCode, Intent? Data)>();

        void Handler(object? sender, ActivityResultEventArgs args)
        {
            if (args.RequestCode != requestCode)
                return;

            MainActivity.ActivityResultReceived -= Handler;
            tcs.TrySetResult((args.ResultCode, args.Data));
        }

        MainActivity.ActivityResultReceived += Handler;
        activity.StartActivityForResult(intent, requestCode);
        return tcs.Task;
    }
}
