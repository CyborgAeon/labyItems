using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class GuildsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new GuildAvailabilityRaceConverter() }
    };

    private static Dictionary<string, GuildRecord>? _cache;

    public static async Task<Dictionary<string, GuildRecord>> GetAllAsync()
    {
        if (_cache != null) return _cache;
        using var s = await FileSystem.OpenAppPackageFileAsync("people/guilds.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();

        _cache = JsonSerializer.Deserialize<Dictionary<string, GuildRecord>>(json, _jsonOptions)
                 ?? new Dictionary<string, GuildRecord>();

        return _cache;
    }

    public static async Task<IReadOnlyList<string>> GetGuildNamesAsync()
    {
        var all = await GetAllAsync();
        var keys = new List<string>(all.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);
        return keys;
    }

    public static async Task<IReadOnlyList<string>> GetTypesAsync()
    {
        var all = await GetAllAsync();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in all)
        {
            var t = kv.Value?.Type?.Trim();
            if (!string.IsNullOrWhiteSpace(t))
                set.Add(t);
        }

        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    public static AlignmentRule? GetAlignmentRule(GuildRecord? record)
    {
        if (record == null)
            return null;

        if (record.AlignmentRule != null)
            return record.AlignmentRule;

        return BuildAlignmentRuleFromAvailability(record.Availability?.Whitelist?.Alignments);
    }

    private static AlignmentRule? BuildAlignmentRuleFromAvailability(GuildAvailabilityAlignments? align)
    {
        if (align == null)
            return null;

        var orders = ParseOrders(align.Order);
        var morals = ParseMorals(align.Moral);
        if (orders.Count == 0 && morals.Count == 0)
            return null;

        return new AlignmentRule
        {
            Mode = "restrict",
            Allowed = new AllowedAxes
            {
                Order = orders.ToList(),
                Moral = morals.ToList()
            }
        };
    }

    private static HashSet<OrderAxis> ParseOrders(IEnumerable<string>? values)
    {
        var set = new HashSet<OrderAxis>();
        foreach (var v in values ?? Array.Empty<string>())
        {
            if (Enum.TryParse<OrderAxis>(v, true, out var parsed))
                set.Add(parsed);
        }
        return set;
    }

    private static HashSet<MoralAxis> ParseMorals(IEnumerable<string>? values)
    {
        var set = new HashSet<MoralAxis>();
        foreach (var v in values ?? Array.Empty<string>())
        {
            if (Enum.TryParse<MoralAxis>(v, true, out var parsed))
                set.Add(parsed);
        }
        return set;
    }
}

public sealed class GuildRecord
{
    public string Type { get; set; } = "";
    public string Restrictions { get; set; } = "";

    public GuildBenefits Benefits { get; set; } = new();

    [JsonPropertyName("alignmentRule")]
    public AlignmentRule? AlignmentRule { get; set; }

    public Dictionary<string, List<string>> MiracleList { get; set; } = new();

    public GuildAvailability Availability { get; set; } = new();
}

public sealed class GuildBenefits
{
    public List<AbilityDefinition> Basic { get; set; } = new();
    public List<AbilityDefinition> Intermediate { get; set; } = new();
    public List<AbilityDefinition> Advanced { get; set; } = new();
}

public sealed class GuildAvailability
{
    public GuildAvailabilityRules Whitelist { get; set; } = new();
    public GuildAvailabilityRules Blacklist { get; set; } = new();
    public string? RequiredGuild { get; set; }
}

public sealed class GuildAvailabilityRules
{
    public List<string> Classes { get; set; } = new();
    public List<string> Brackets { get; set; } = new();
    public GuildAvailabilityAlignments Alignments { get; set; } = new();
    public List<GuildAvailabilityRace> Races { get; set; } = new();
    public List<string> PeopleType { get; set; } = new();
}

public sealed class GuildAvailabilityAlignments
{
    public List<string> Order { get; set; } = new();
    public List<string> Moral { get; set; } = new();
}

public sealed class GuildAvailabilityRace
{
    public string Name { get; set; } = "";
    public string? Subtype { get; set; }
}

public sealed class GuildAvailabilityRaceConverter : JsonConverter<GuildAvailabilityRace>
{
    public override GuildAvailabilityRace Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new GuildAvailabilityRace { Name = reader.GetString() ?? string.Empty };

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Unexpected token {reader.TokenType} when parsing guild race.");

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var race = new GuildAvailabilityRace
        {
            Name = root.TryGetProperty("Name", out var nameEl) ? (nameEl.GetString() ?? string.Empty) : string.Empty
        };

        if (root.TryGetProperty("Subtype", out var subtypeEl))
            race.Subtype = subtypeEl.GetString();
        else if (root.TryGetProperty("Clan", out var clanEl))
            race.Subtype = clanEl.GetString();

        return race;
    }

    public override void Write(Utf8JsonWriter writer, GuildAvailabilityRace value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Name", value?.Name ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(value?.Subtype))
            writer.WriteString("Subtype", value!.Subtype);
        writer.WriteEndObject();
    }
}
