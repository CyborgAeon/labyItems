using labyItems.Services;

namespace labyItems.Models.NonStandard;

public sealed record NonStandardDocumentLink(
    string Id,
    NonStandardEntityType EntityType,
    string EntityName,
    string DisplayName,
    string FilePath,
    string FileKind,
    string? ContentType,
    string StorageKind,
    string PersistentReference,
    string? SourceUri,
    string? AccessReference,
    DateTimeOffset UpdatedAtUtc)
{
    public string OwnerAccountKey { get; init; } = string.Empty;
}

