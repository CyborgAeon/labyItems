namespace labyItems.Services.Guilds;

public static class GuildMiracleAccessResolver
{
    public static (string? Guild, IReadOnlyDictionary<string, IReadOnlyList<string>>? List) ResolveMembership(
        IEnumerable<string>? memberships,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> lists)
    {
        foreach (var guild in memberships ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(guild) && lists.TryGetValue(guild, out var list) && list.Count > 0)
                return (guild, list);
        }

        return (null, null);
    }
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> AddOrdinaryAccess(
        IReadOnlyDictionary<string, IReadOnlyList<string>> list,
        IEnumerable<string> additions,
        Func<string, (string Name, int Level)?> resolveOrdinaryMiracle)
    {
        var existing = new HashSet<string>(list.Values.SelectMany(names => names), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IReadOnlyList<string>>? effective = null;
        foreach (var reference in additions)
        {
            if (resolveOrdinaryMiracle(reference) is not { } miracle || !existing.Add(miracle.Name))
                continue;

            effective ??= new Dictionary<string, IReadOnlyList<string>>(list);
            var level = miracle.Level.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var names = effective.GetValueOrDefault(level) ?? Array.Empty<string>();
            effective[level] = names.Append(miracle.Name).ToArray();
        }

        return effective ?? list;
    }
}
