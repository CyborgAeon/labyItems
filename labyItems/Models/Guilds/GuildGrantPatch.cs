using labyItems.Models.Characters;

namespace labyItems.Models.Guilds;

public sealed record GuildGrantPatch
{
    public int? Select { get; init; }
    public bool GrantAll { get; init; }
    public int? Count { get; init; }
    public int? CountDelta { get; init; }
    public IReadOnlyList<int>? Amount { get; init; }
    public string? DescriptionRef { get; init; }
    public GuildGrantOverrides? Overrides { get; init; }
    public IReadOnlyDictionary<string, GuildGrantOverrides>? OptionOverrides { get; init; }
}
