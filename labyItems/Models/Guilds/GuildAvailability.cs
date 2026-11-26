using labyItems.Models.Rules;

namespace labyItems.Models.Guilds;

public sealed class GuildAvailability
{
    public List<RuleClause> Rules { get; set; } = new();
    public string? RequiredGuild { get; set; }
}
