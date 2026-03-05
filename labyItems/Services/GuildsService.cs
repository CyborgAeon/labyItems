using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using labyItems.Models.Characters;

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
        var json = await ServiceHelper.ReadPackageTextAsync("people/guilds.json");

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
    public List<GuildBenefitEntry> Basic { get; set; } = new();
    public List<GuildBenefitEntry> Intermediate { get; set; } = new();
    public List<GuildBenefitEntry> Advanced { get; set; } = new();
}

public static class GuildBenefitKeys
{
    public static string BuildSelectionKey(string guildName, string tier, int optionIndex)
    {
        var guild = (guildName ?? string.Empty).Trim();
        var level = (tier ?? string.Empty).Trim();
        return $"{guild}::{level}::{optionIndex}";
    }
}

[JsonConverter(typeof(GuildBenefitEntryConverter))]
public sealed class GuildBenefitEntry
{
    public AbilityDefinition? Ability { get; set; }
    public List<GuildBenefitOption> Options { get; set; } = new();
    public bool IsOptionGroup => Options.Count > 0;
}

public sealed class GuildBenefitOption
{
    public List<AbilityDefinition> Abilities { get; set; } = new();
}

public sealed class GuildBenefitEntryConverter : JsonConverter<GuildBenefitEntry>
{
    public override GuildBenefitEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return ParseEntry(doc.RootElement, options);
    }

    private static GuildBenefitEntry ParseEntry(JsonElement el, JsonSerializerOptions options)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                var entry = new GuildBenefitEntry();
                foreach (var optionEl in el.EnumerateArray())
                {
                    var abilities = new List<AbilityDefinition>();
                    AddAbilitiesFromElement(optionEl, abilities, options);
                    if (abilities.Count > 0)
                        entry.Options.Add(new GuildBenefitOption { Abilities = abilities });
                }
                return entry;
            case JsonValueKind.Object:
            case JsonValueKind.String:
                var ability = ParseAbility(el, options);
                return new GuildBenefitEntry { Ability = ability };
            default:
                return new GuildBenefitEntry();
        }
    }

    private static void AddAbilitiesFromElement(JsonElement element, List<AbilityDefinition> abilities, JsonSerializerOptions options)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                AddAbilitiesFromElement(child, abilities, options);
            return;
        }

        var ability = ParseAbility(element, options);
        if (ability != null && !string.IsNullOrWhiteSpace(ability.Name))
            abilities.Add(ability);
    }

    private static AbilityDefinition? ParseAbility(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Object && element.ValueKind != JsonValueKind.String)
            return null;

        try
        {
            return JsonSerializer.Deserialize<AbilityDefinition>(element.GetRawText(), options);
        }
        catch
        {
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, GuildBenefitEntry value, JsonSerializerOptions options)
    {
        if (value?.Ability != null)
        {
            JsonSerializer.Serialize(writer, value.Ability, options);
            return;
        }

        writer.WriteStartArray();
        foreach (var option in value?.Options ?? new List<GuildBenefitOption>())
        {
            writer.WriteStartArray();
            foreach (var ability in option.Abilities ?? new List<AbilityDefinition>())
                JsonSerializer.Serialize(writer, ability, options);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }
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
