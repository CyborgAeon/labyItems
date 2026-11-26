using labyItems.Models.Characters;

namespace labyItems.Models.Guilds;

public sealed class GuildBenefitOption
{
    public List<AbilityDefinition> Abilities { get; set; } = new();
}
