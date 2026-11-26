using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models.Guilds;

[JsonConverter(typeof(GuildMiracleReferenceConverter))]
public sealed class GuildMiracleReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;
}
