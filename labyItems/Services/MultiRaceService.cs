using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Characters;
using labyItems.Models.Rules;

namespace labyItems.Services;

public static class MultiRaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly SemaphoreSlim LoadLock = new(1, 1);
    private static MultiRaceCatalog? _cache;

    public static async Task<MultiRaceCatalog> GetCatalogAsync()
    {
        var session = LabyrintheSession.Shared;
        var version = session.Version;
        if (_cache is { } existing)
            return existing;

        await LoadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            session.RequireVersion(version);
            if (_cache is { } cached)
                return cached;

            var catalog = await LoadCatalogueAsync().ConfigureAwait(false);
            session.Publish(version, () => _cache = catalog);
            return catalog;
        }
        finally
        {
            LoadLock.Release();
        }
    }

    private static async Task<MultiRaceCatalog> LoadCatalogueAsync()
    {
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("people/multi-races.json");
            var catalog = JsonSerializer.Deserialize<MultiRaceCatalog>(json, JsonOptions)
                ?? new MultiRaceCatalog();
            if (catalog.SchemaVersion == 2)
                await MultiPathCatalogueAbilityResolver.BindAsync(catalog.MultiRaces.Values
                    .SelectMany(definition => definition.Levels.Values)
                    .SelectMany(level => level)).ConfigureAwait(false);
            return catalog;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MultiRaceService] Catalogue load failed: {ex.Message}");
            return new MultiRaceCatalog();
        }
    }

    public static void InvalidateCache()
        => _cache = null;
}

public sealed class MultiRaceCatalog
{
    [JsonPropertyName("$schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("metadata")]
    public JsonElement Metadata { get; set; }

    [JsonPropertyName("multiRaces")]
    public Dictionary<string, MultiRaceDefinition> MultiRaces { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MultiRaceDefinition :
    IMultiPathDefinition<MultiRaceAvailabilityOption, MultiRaceLevelAbility, MultiRaceSystemEffect>
{
    public string DisplayName { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public int MaxLevel { get; set; }
    public Dictionary<string, List<MultiRaceLevelAbility>> Levels { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public List<MultiRaceAvailabilityOption> AvailabilityOptions { get; set; } = new();
    public Dictionary<string, List<MultiRaceSystemEffect>> SystemEffectsByLevel { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Notes { get; set; } = new();
    public string? RequiresBracketPure { get; set; }
    public string? RacialProgressionRef { get; set; }
    public int? InnateLevelOverride { get; set; }
}

public sealed class MultiRaceAvailabilityOption : IMultiPathAvailabilityOption
{
    public string Source { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public List<RuleClause> Rules { get; set; } = new();
    public Dictionary<string, int> CostsByLevel { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> PreReqs { get; set; } = new();
    public JsonElement Notes { get; set; }
}

public sealed class MultiRaceSystemEffect : IMultiPathSystemEffect<MultiRaceLifeScaleReference>
{
    public string EffectType { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
    public MultiRaceLifeScaleReference? LifeScaleReference { get; set; }
    public List<RuleClause> Conditions { get; set; } = new();
    public string SourceCategory { get; set; } = string.Empty;
    public List<string> LinkedAbilityRefs { get; set; } = new();
}

public sealed class MultiRaceLifeScaleReference : IMultiPathLifeScaleReference
{
    public string File { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int Level { get; set; }
}
