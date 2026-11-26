using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Guilds;

namespace labyItems.Services.Guilds;

internal static class GuildCatalogue
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Lazy<Task<IReadOnlyDictionary<string, GuildRecord>>> Records = new(LoadPackagedRecordsAsync);

    internal static Task<IReadOnlyDictionary<string, GuildRecord>> GetAllAsync() => Records.Value;

    private static async Task<IReadOnlyDictionary<string, GuildRecord>> LoadPackagedRecordsAsync()
    {
        var json = await ServiceHelper.ReadPackageTextAsync("people/guilds.json").ConfigureAwait(false);
        return await Task.Run(() =>
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("$schemaVersion", out var version))
            {
                if (version.GetInt32() is not (2 or 3))
                    throw new JsonException("Unsupported guild schema.");
                root = root.GetProperty("guilds");
            }

            return root.EnumerateObject().ToFrozenDictionary(
                guild => guild.Name,
                guild => guild.Value.Deserialize<GuildRecord>(JsonOptions)
                    ?? throw new JsonException($"Missing guild definition for {guild.Name}."),
                StringComparer.OrdinalIgnoreCase);
        }).ConfigureAwait(false);
    }
}
