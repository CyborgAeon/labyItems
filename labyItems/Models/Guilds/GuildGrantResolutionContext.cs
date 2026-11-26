namespace labyItems.Models.Guilds;

public sealed record GuildGrantResolutionContext
{
    public string GuildName { get; init; } = "";
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Selections { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, IReadOnlyList<string>> AdventureSelections { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, int> LegacySelections { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> CompletedAdventureMigrations { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public Func<string, IEnumerable<string>?> ResolveRuleField { get; init; } = _ => null;
    public Func<string, IReadOnlyList<GuildGrantOption>?> ResolveChoiceSet { get; init; } = _ => null;
}
