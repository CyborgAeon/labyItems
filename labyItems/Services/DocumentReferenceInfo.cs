namespace labyItems.Services;

public sealed record DocumentReferenceInfo(
    string StorageKind,
    string PersistentReference,
    string? SourceUri,
    string? AccessReference,
    string? DisplayPath,
    string? FileName,
    string? ContentType)
{
    public bool HasReference
        => !string.IsNullOrWhiteSpace(PersistentReference)
           || !string.IsNullOrWhiteSpace(SourceUri)
           || !string.IsNullOrWhiteSpace(DisplayPath);

    public static DocumentReferenceInfo FromCapture(DocumentReferenceCapture capture)
        => new(
            capture.StorageKind,
            capture.PersistentReference,
            capture.SourceUri,
            capture.AccessReference,
            capture.DisplayPath,
            capture.FileName,
            capture.ContentType);
}
