namespace labyItems.Models.Documents;

public sealed record DocumentReferenceCapture(
    string StorageKind,
    string PersistentReference,
    string? SourceUri,
    string? AccessReference,
    string? DisplayPath,
    string? FileName,
    string? ContentType);
