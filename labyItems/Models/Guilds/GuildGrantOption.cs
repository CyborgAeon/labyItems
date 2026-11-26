using System.Text.Json;
using labyItems.Models.Rules;

namespace labyItems.Models.Guilds;

public sealed record GuildGrantOption
{
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    public bool Repeatable { get; init; }
    public IReadOnlyList<GuildGrantNode> Grants { get; init; } = [];
    public IReadOnlyList<RuleClause> Rules { get; init; } = [];

    internal static GuildGrantOption Parse(JsonElement element, JsonSerializerOptions options)
        => new()
        {
            Id = element.GetProperty("Id").GetString() ?? "",
            Name = element.TryGetProperty("Name", out var name) ? name.GetString() : null,
            Repeatable = element.TryGetProperty("Repeatable", out var repeatable) && repeatable.GetBoolean(),
            Grants = GuildGrantNode.ReadGrants(element, options),
            Rules = GuildGrantNode.ReadRules(element, options)
        };
}
