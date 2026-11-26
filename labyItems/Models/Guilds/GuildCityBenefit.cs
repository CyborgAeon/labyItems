using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models.Guilds;

[JsonConverter(typeof(GuildCityBenefitConverter))]
public sealed class GuildCityBenefit
{
    public string Name { get; set; } = string.Empty;
    public List<string> Effects { get; set; } = new();
}
