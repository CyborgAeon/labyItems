using System.Collections.Frozen;
using System.Text.Json;

namespace labyItems.Services.Guilds;

public static class GuildMiracleListCatalogue
{
    private static readonly Lazy<Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>>> Lists =
        new(LoadAsync);

    public static Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>> GetAllAsync()
        => Lists.Value;

    private static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>> LoadAsync()
    {
        var json = await ServiceHelper.ReadPackageTextAsync("people/miracleLists.json").ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("$schemaVersion").GetInt32() != 1)
            throw new JsonException("Unsupported guild miracle-list schema.");

        return root.GetProperty("miracleLists").EnumerateObject().ToFrozenDictionary(
            guild => guild.Name,
            guild => (IReadOnlyDictionary<string, IReadOnlyList<string>>)guild.Value.EnumerateObject().ToFrozenDictionary(
                level => level.Name,
                level => (IReadOnlyList<string>)Array.AsReadOnly(
                    level.Value.Deserialize<string[]>() ?? throw new JsonException("A miracle level must contain a list."))),
            StringComparer.OrdinalIgnoreCase);
    }
}
