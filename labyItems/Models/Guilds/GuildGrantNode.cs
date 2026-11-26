using System.Text.Json;
using labyItems.Models.Characters;
using labyItems.Models.Rules;

namespace labyItems.Models.Guilds;

public sealed record GuildGrantNode
{
    public string Kind { get; init; } = "Grant";
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    public string? Mode { get; init; }
    public string? SelectionRef { get; init; }
    public string? LegacySelectionKey { get; init; }
    public IReadOnlyList<string> LegacyOptionIds { get; init; } = [];
    public string? Timing { get; init; }
    public string? TargetGrantId { get; init; }
    public string? MiracleKey { get; init; }
    public int? Select { get; init; }
    public bool DistinctAcrossRepeats { get; init; }
    public AbilityDefinition Ability { get; init; } = new();
    public GuildGrantNode? Choice { get; init; }
    public GuildGrantNode? Replacement { get; init; }
    public GuildGrantPatch? Patch { get; init; }
    public IReadOnlyList<GuildGrantOption> Options { get; init; } = [];
    public IReadOnlyList<GuildGrantNode> Grants { get; init; } = [];
    public IReadOnlyList<RuleClause> Rules { get; init; } = [];
    public IReadOnlyList<string> ChoiceSetRefs { get; init; } = [];

    public static GuildGrantNode Parse(JsonElement element, JsonSerializerOptions options)
    {
        string? Read(string name) => element.TryGetProperty(name, out var value) ? value.GetString() : null;
        T? ReadValue<T>(string name) => element.TryGetProperty(name, out var value)
            ? value.Deserialize<T>(options)
            : default;
        GuildGrantNode? ReadNode(string name) => element.TryGetProperty(name, out var value)
            ? Parse(value, options)
            : null;

        return new GuildGrantNode
        {
            Kind = Read("Kind") ?? "Grant",
            Id = Read("Id") ?? "",
            Name = Read("Name"),
            Mode = Read("Mode"),
            SelectionRef = Read("SelectionRef"),
            LegacySelectionKey = Read("LegacySelectionKey"),
            LegacyOptionIds = ReadValue<string[]>("LegacyOptionIds") ?? [],
            Timing = Read("Timing"),
            TargetGrantId = Read("TargetGrantId"),
            MiracleKey = Read("MiracleKey"),
            Select = ReadValue<int?>("Select"),
            DistinctAcrossRepeats = ReadValue<bool>("DistinctAcrossRepeats"),
            Ability = element.Deserialize<AbilityDefinition>(options) ?? new AbilityDefinition(),
            Choice = ReadNode("Choice"),
            Replacement = ReadNode("Replacement"),
            Patch = ReadValue<GuildGrantPatch>("Patch"),
            Options = element.TryGetProperty("Options", out var choices)
                ? choices.EnumerateArray().Select(option => GuildGrantOption.Parse(option, options)).ToArray()
                : [],
            Grants = ReadGrants(element, options),
            Rules = ReadRules(element, options),
            ChoiceSetRefs = ReadValue<string[]>("ChoiceSetRefs") ?? []
        };
    }

    internal static GuildGrantNode[] ReadGrants(JsonElement element, JsonSerializerOptions options)
        => element.TryGetProperty("Grants", out var grants)
            ? grants.EnumerateArray().Select(grant => Parse(grant, options)).ToArray()
            : [];

    internal static RuleClause[] ReadRules(JsonElement element, JsonSerializerOptions options)
        => element.TryGetProperty("Rules", out var rules)
            ? rules.Deserialize<RuleClause[]>(options) ?? []
            : [];
}
