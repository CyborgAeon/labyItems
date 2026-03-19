using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using SQLite;

namespace labyItems.Services;

public static class PeopleService
{

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new GuildOverrideRulesConverter() }
    };

    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static Dictionary<string, PeopleRecord>? _cache;

    public static async Task<Dictionary<string, PeopleRecord>> GetAllAsync()
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
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get races", ex);
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

    private static Dictionary<string, PeopleRecord> LoadFromDb()
    {
        using var conn = ServiceHelper.OpenReadOnlyConnection();
        var rows = conn.Query<PeopleRow>("SELECT name, data_json FROM races ORDER BY name;");
        var dict = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.name))
                continue;

            var record = string.IsNullOrWhiteSpace(row.data_json)
                ? new PeopleRecord()
                : (JsonSerializer.Deserialize<PeopleRecord>(row.data_json, _jsonOptions) ?? new PeopleRecord());

            dict[row.name] = record;
        }

        return dict;
    }

    private static async Task<Dictionary<string, PeopleRecord>> LoadDebugMergedAsync()
    {
        var dict = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("people/people.json").ConfigureAwait(false);
            var packaged = await Task.Run(() =>
                    JsonSerializer.Deserialize<Dictionary<string, PeopleRecord>>(json, _jsonOptions)
                    ?? new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase))
                .ConfigureAwait(false);
            foreach (var kvp in packaged)
                dict[kvp.Key] = kvp.Value;
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get races (debug package)", ex);
        }

        try
        {
            var fromDb = await Task.Run(LoadFromDb).ConfigureAwait(false);
            foreach (var kvp in fromDb)
                dict[kvp.Key] = kvp.Value;
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get races (debug db merge)", ex);
        }

        return dict;
    }
}

internal sealed class PeopleRow
{
    public string name { get; set; } = string.Empty;
    public string? data_json { get; set; }
}
public sealed class PeopleRecord
{
    public bool NonStandard { get; set; }

    [JsonConverter(typeof(SingleOrArrayStringListConverter))]
    public List<string> PeopleType { get; set; } = new();
    [JsonConverter(typeof(SingleOrArrayStringListConverter))]
    public List<string> Tags { get; set; } = new();
    public string Description { get; set; } = "";

    [JsonPropertyName("levelledAbilities")]
    public Dictionary<string, List<AbilityDefinition>> LevelledAbilities { get; set; } = new();

    public string? AdditionalInfo { get; set; }

    public PeopleSubtypeRecord? Subtype { get; set; }

    public GuildOverrideRules? GuildOverrides { get; set; }

    // legacy fields you may still have in older JSON
    [JsonPropertyName("Buy-as")]
    public string? BuyAs { get; set; }
    [JsonPropertyName("alignmentRule")]
    public AlignmentRule? AlignmentRule { get; set; }
}

public sealed class PeopleSubtypeRecord
{
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";

    // e.g. "SingleRequired"
    public string SelectionMode { get; set; } = "SingleOptional";

    // e.g. "Enum:ElfColours"
    public string OptionsSource { get; set; } = "";

    // e.g. "ElfColourAbilities"
    public string AbilityMapKey { get; set; } = "";
}

public sealed class SingleOrArrayStringListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        list.Add(value);
                }
            }
            return list;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : new List<string> { value };
        }

        if (reader.TokenType == JsonTokenType.Null)
            return new List<string>();

        throw new JsonException($"Unexpected token {reader.TokenType} when parsing PeopleType.");
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var entry in value ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(entry))
                writer.WriteStringValue(entry);
        }
        writer.WriteEndArray();
    }
}
