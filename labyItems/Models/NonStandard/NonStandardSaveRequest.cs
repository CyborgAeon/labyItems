using labyItems.Services;

namespace labyItems.Models.NonStandard;

public sealed class NonStandardSaveRequest
{
    public string OwnerAccountKey { get; init; } = string.Empty;
    public NonStandardEntityType EntityType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public string? LifeScaleRaceName { get; set; }
    public string? LifeScaleClassName { get; set; }
    public IReadOnlyList<LifeScalePoint>? LifeScalePoints { get; set; }
    public IReadOnlyList<NonStandardLifeScaleAssignment>? LifeScaleAssignments { get; set; }
    public IReadOnlyList<RaceLifeScaleClassEntry>? RaceLifeScaleEntries { get; set; }
    public string? AssignedCharacterId { get; set; }
    public string? AssignedCharacterName { get; set; }
    public string? AssignedCharacterPlayerName { get; set; }
}
