namespace labyItems.Services;

public static class DocumentReferenceKinds
{
    public const string LegacyPath = "legacy-path";
    public const string LocalPath = "local-path";
    public const string AndroidDocumentUri = "android-document-uri";
    public const string AndroidMediaUri = "android-media-uri";
    public const string IosSecurityScopedBookmark = "ios-security-scoped-bookmark";
}

public sealed record DocumentReferenceCapture(
    string StorageKind,
    string PersistentReference,
    string? SourceUri,
    string? AccessReference,
    string? DisplayPath,
    string? FileName,
    string? ContentType);

public interface IDocumentReferenceService
{
    Task<DocumentReferenceCapture?> PickDocumentAsync();
    Task<DocumentReferenceCapture?> PickImageAsync();
    Task<DocumentReferenceCapture?> CapturePhotoAsync();
    Task<bool> IsAvailableAsync(NonStandardDocumentLink document);
    Task OpenAsync(NonStandardDocumentLink document);
}
