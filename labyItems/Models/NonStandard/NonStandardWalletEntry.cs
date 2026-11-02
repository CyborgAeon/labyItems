using labyItems.Services;

namespace labyItems.Models.NonStandard;

public sealed record NonStandardWalletEntry(
    NonStandardEntityType EntityType,
    string Name,
    string DataJson,
    string Subtitle)
{
    public string OwnerAccountKey { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
