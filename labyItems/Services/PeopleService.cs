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

            // #if DEBUG
            //             _cache = await LoadDebugMergedAsync().ConfigureAwait(false);
            // #else
            _cache = await Task.Run(LoadFromDb).ConfigureAwait(false);
            // #endif
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
            {
                if (!dict.TryGetValue(kvp.Key, out var packaged))
                {
                    dict[kvp.Key] = kvp.Value;
                    continue;
                }

                if (kvp.Value.NonStandard)
                {
                    dict[kvp.Key] = kvp.Value;
                    continue;
                }

                dict[kvp.Key] = MergePreferPackaged(packaged, kvp.Value);
            }
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get races (debug db merge)", ex);
        }

        return dict;
    }

    private static PeopleRecord MergePreferPackaged(PeopleRecord packaged, PeopleRecord fromDb)
    {
        var merged = ClonePeopleRecord(packaged);
        merged.NonStandard = packaged.NonStandard || fromDb.NonStandard;

        if (merged.PeopleType.Count == 0 && fromDb.PeopleType.Count > 0)
            merged.PeopleType = new List<string>(fromDb.PeopleType);
        if (merged.Tags.Count == 0 && fromDb.Tags.Count > 0)
            merged.Tags = new List<string>(fromDb.Tags);
        if (string.IsNullOrWhiteSpace(merged.Description) && !string.IsNullOrWhiteSpace(fromDb.Description))
            merged.Description = fromDb.Description;
        if (merged.LevelledAbilities.Count == 0 && fromDb.LevelledAbilities.Count > 0)
            merged.LevelledAbilities = CloneLevelledAbilities(fromDb.LevelledAbilities);
        if (string.IsNullOrWhiteSpace(merged.AdditionalInfo) && !string.IsNullOrWhiteSpace(fromDb.AdditionalInfo))
            merged.AdditionalInfo = fromDb.AdditionalInfo;
        if (merged.Subtype == null && fromDb.Subtype != null)
            merged.Subtype = CloneSubtype(fromDb.Subtype);
        if (merged.GuildOverrides == null && fromDb.GuildOverrides != null)
            merged.GuildOverrides = fromDb.GuildOverrides;
        if (string.IsNullOrWhiteSpace(merged.BuyAs) && !string.IsNullOrWhiteSpace(fromDb.BuyAs))
            merged.BuyAs = fromDb.BuyAs;
        if (merged.AlignmentRule == null && fromDb.AlignmentRule != null)
            merged.AlignmentRule = fromDb.AlignmentRule;

        return merged;
    }

    private static PeopleRecord ClonePeopleRecord(PeopleRecord source)
    {
        return new PeopleRecord
        {
            NonStandard = source.NonStandard,
            PeopleType = new List<string>(source.PeopleType ?? new List<string>()),
            Tags = new List<string>(source.Tags ?? new List<string>()),
            Description = source.Description ?? string.Empty,
            LevelledAbilities = CloneLevelledAbilities(source.LevelledAbilities),
            AdditionalInfo = source.AdditionalInfo,
            Subtype = source.Subtype == null ? null : CloneSubtype(source.Subtype),
            GuildOverrides = source.GuildOverrides,
            BuyAs = source.BuyAs,
            AlignmentRule = source.AlignmentRule
        };
    }

    private static Dictionary<string, List<AbilityDefinition>> CloneLevelledAbilities(
        Dictionary<string, List<AbilityDefinition>> source)
    {
        var clone = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase);
        if (source == null)
            return clone;

        foreach (var entry in source)
        {
            var abilities = new List<AbilityDefinition>();
            if (entry.Value != null)
            {
                foreach (var ability in entry.Value)
                {
                    if (ability == null)
                        continue;

                    abilities.Add(new AbilityDefinition
                    {
                        Key = ability.Key,
                        Name = ability.Name,
                        Type = ability.Type,
                        Effect = ability.Effect,
                        Lore = ability.Lore,
                        BattleboardNameOverride = ability.BattleboardNameOverride,
                        UpdateKey = ability.UpdateKey,
                        Source = ability.Source,
                        Count = ability.Count,
                        Amount = ability.Amount == null ? null : new List<int>(ability.Amount),
                        Frequency = ability.Frequency,
                        OverwriteKey = ability.OverwriteKey,
                        PreReqs = ability.PreReqs == null ? null : new List<string>(ability.PreReqs),
                        GuildOverrides = ability.GuildOverrides == null ? null : new List<string>(ability.GuildOverrides),
                        AbilityRef = ability.AbilityRef,
                        Customisation = ability.Customisation == null
                            ? null
                            : new AbilityCustomisation
                            {
                                OptionEnum = ability.Customisation.OptionEnum,
                                CustomValuesPermitted = ability.Customisation.CustomValuesPermitted
                            }
                    });
                }
            }

            clone[entry.Key] = abilities;
        }

        return clone;
    }

    private static PeopleSubtypeRecord CloneSubtype(PeopleSubtypeRecord source)
    {
        return new PeopleSubtypeRecord
        {
            Key = source.Key ?? string.Empty,
            DisplayName = source.DisplayName ?? string.Empty,
            Description = source.Description ?? string.Empty,
            SelectionMode = source.SelectionMode ?? "SingleOptional",
            OptionsSource = source.OptionsSource ?? string.Empty,
            AbilityMapKey = source.AbilityMapKey ?? string.Empty
        };
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
