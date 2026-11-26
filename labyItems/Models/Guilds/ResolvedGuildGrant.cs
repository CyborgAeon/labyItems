using labyItems.Models.Characters;

namespace labyItems.Models.Guilds;

public sealed record ResolvedGuildGrant(
    string Id,
    string InstanceId,
    AbilityDefinition Ability,
    IReadOnlyList<GuildGrantPatch> Patches);
