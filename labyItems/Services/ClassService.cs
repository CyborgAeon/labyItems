using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using SQLite;

namespace labyItems.Services;

public static class ClassService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new GuildOverrideRulesConverter() }
    };

    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static Dictionary<string, CharacterClassRecord>? _cache;

    public static async Task<Dictionary<string, CharacterClassRecord>> GetAllAsync()
    {
        if (_cache != null)
            return _cache;

        await CacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cache != null)
                return _cache;

#if DEBUG
            _cache = await LoadDebugMergedAsync().ConfigureAwait(false);
#else
            _cache = await Task.Run(LoadFromDb).ConfigureAwait(false);
#endif
            if (_cache.Count == 0)
            {
                _cache = null;
                throw new InvalidOperationException("No class definitions were available from either the packaged data or the database.");
            }
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get classes", ex);
            throw;
        }
        finally
        {
            CacheLock.Release();
        }

        return _cache;
    }

    public static void InvalidateCache()
        => _cache = null;

    private static Dictionary<string, CharacterClassRecord> LoadFromDb()
    {
        using var conn = ServiceHelper.OpenReadOnlyConnection();
        var rows = conn.Query<ClassRow>("SELECT name, data_json FROM classes ORDER BY name;");
        var dict = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.name))
                continue;

            var record = string.IsNullOrWhiteSpace(row.data_json)
                ? new CharacterClassRecord()
                : DeserializeClassRecord(row.data_json);

            dict[row.name] = record;
        }

        return dict;
    }

    private static async Task<Dictionary<string, CharacterClassRecord>> LoadDebugMergedAsync()
    {
        var dict = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("people/classes.json").ConfigureAwait(false);
            var packaged = await Task.Run(() => ParsePackagedClassDictionary(json)).ConfigureAwait(false);
            foreach (var kvp in packaged)
                dict[kvp.Key] = kvp.Value;
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get classes (debug package)", ex);
        }

        try
        {
            var fromDb = await Task.Run(LoadFromDb).ConfigureAwait(false);
            foreach (var kvp in fromDb)
                dict[kvp.Key] = kvp.Value;
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get classes (debug db merge)", ex);
        }

        return dict;
    }

    private static Dictionary<string, CharacterClassRecord> ParsePackagedClassDictionary(string json)
    {
        var dict = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return dict;

        foreach (var cls in doc.RootElement.EnumerateObject())
        {
            if (cls.Value.ValueKind != JsonValueKind.Object)
                continue;

            dict[cls.Name] = DeserializeClassRecord(cls.Value);
        }

        return dict;
    }

    private static CharacterClassRecord DeserializeClassRecord(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CharacterClassRecord();

        using var doc = JsonDocument.Parse(json);
        return DeserializeClassRecord(doc.RootElement);
    }

    private static CharacterClassRecord DeserializeClassRecord(JsonElement element)
        => JsonSerializer.Deserialize<CharacterClassRecord>(element.GetRawText(), _jsonOptions)
           ?? new CharacterClassRecord();

    private sealed class ClassRow
    {
        public string name { get; set; } = string.Empty;
        public string? data_json { get; set; }
    }
}

public sealed class CharacterClassRecord
{
    [JsonPropertyName("SourceBook")]
    public string? SourceBook { get; set; }
    public string? Lifescale { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public bool NonStandard { get; set; }
    public List<string> Brackets { get; set; } = new();
    public Dictionary<string, List<AbilityDefinition>> Levels { get; set; } = new();

    public ArmourAllowance? Armour { get; set; }

    [JsonPropertyName("Max AC")]
    public JsonElement MaxAC { get; set; }

    public List<string>? Powerbase { get; set; }

    public JsonElement PowerPerLevel { get; set; }

    public int? CasterLevel { get; set; }

    public List<PowerCalculation>? PowerCalculations { get; set; }

    [JsonPropertyName("Buy as")]
    public List<string>? BuyAs { get; set; }

    [JsonPropertyName("Path")]
    public string? Path { get; set; }

    [JsonPropertyName("alignmentRule")]
    public AlignmentRule? AlignmentRule { get; set; }

    public GuildOverrideRules? GuildOverrides { get; set; }
}

public sealed class PowerCalculation
{
    public string PowerBase { get; set; } = "";
    public string Calculation { get; set; } = "";
}

public sealed class ArmourAllowance
{
    public List<string> Wearable { get; set; } = new();
    public List<string>? Restrictions { get; set; }
}
