using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Characters;

namespace labyItems.Models.Guilds;

[JsonConverter(typeof(GuildBenefitEntryConverter))]
public sealed class GuildBenefitEntry
{
    public AbilityDefinition? Ability { get; set; }
    public GuildGrantNode? Node { get; set; }
    public List<GuildBenefitOption> Options { get; set; } = new();
    public bool IsOptionGroup => Options.Count > 0;
}
