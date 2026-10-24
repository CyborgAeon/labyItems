namespace labyItems.Models.Search;

internal sealed class FilterSnapshot
{
    public HashSet<GlobalSearchKind> Kinds { get; init; } = new();
    public HashSet<string> SpellColours { get; init; } = NewSet();
    public HashSet<string> SpellTiers { get; init; } = NewSet();
    public HashSet<string> AbilityTables { get; init; } = NewSet();
    public HashSet<string> MiracleSpheres { get; init; } = NewSet();
    public HashSet<string> MiracleTiers { get; init; } = NewSet();
    public HashSet<string> EvocationFields { get; init; } = NewSet();
    public HashSet<string> EvocationTiers { get; init; } = NewSet();
    public HashSet<string> NeuroTypes { get; init; } = NewSet();

    public static FilterSnapshot Empty() => new();

    private static HashSet<string> NewSet(IEnumerable<string>? source = null)
        => new(source ?? [], StringComparer.OrdinalIgnoreCase);

    public FilterSnapshot Clone() => new()
    {
        Kinds = new HashSet<GlobalSearchKind>(Kinds),
        SpellColours = NewSet(SpellColours),
        SpellTiers = NewSet(SpellTiers),
        AbilityTables = NewSet(AbilityTables),
        MiracleSpheres = NewSet(MiracleSpheres),
        MiracleTiers = NewSet(MiracleTiers),
        EvocationFields = NewSet(EvocationFields),
        EvocationTiers = NewSet(EvocationTiers),
        NeuroTypes = NewSet(NeuroTypes)
    };

    public HashSet<string>? GetTokens(string prefix) => prefix switch
    {
        "spell-colour:" => SpellColours,
        "spell-tier:" => SpellTiers,
        "ability-table:" => AbilityTables,
        "miracle-sphere:" => MiracleSpheres,
        "miracle-tier:" => MiracleTiers,
        "evocation-field:" => EvocationFields,
        "evocation-tier:" => EvocationTiers,
        "neuro-type:" => NeuroTypes,
        _ => null
    };

    public HashSet<string> ToFilterSet()
    {
        var filters = NewSet();
        filters.UnionWith(SpellColours.Select(token => $"spell-colour:{token}"));
        filters.UnionWith(SpellTiers.Select(token => $"spell-tier:{token}"));
        filters.UnionWith(AbilityTables.Select(token => $"ability-table:{token}"));
        filters.UnionWith(MiracleSpheres.Select(token => $"miracle-sphere:{token}"));
        filters.UnionWith(MiracleTiers.Select(token => $"miracle-tier:{token}"));
        filters.UnionWith(EvocationFields.Select(token => $"evocation-field:{token}"));
        filters.UnionWith(EvocationTiers.Select(token => $"evocation-tier:{token}"));
        filters.UnionWith(NeuroTypes.Select(token => $"neuro-type:{token}"));
        return filters;
    }
}
