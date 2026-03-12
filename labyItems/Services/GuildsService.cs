using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using labyItems.Models.Rules;

namespace labyItems.Services;

public static class GuildsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static Dictionary<string, GuildRecord>? _cache;
    private static Dictionary<string, GuildRecord>? _miracleSearchCache;

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

    public static async Task<Dictionary<string, GuildRecord>> GetMiracleSearchAsync()
    {
        if (_miracleSearchCache != null)
            return _miracleSearchCache;

        var json = await ServiceHelper.ReadPackageTextAsync("people/guilds.json");
        using var doc = JsonDocument.Parse(json);
        var map = new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            _miracleSearchCache = map;
            return _miracleSearchCache;
        }

        foreach (var guildProperty in doc.RootElement.EnumerateObject())
        {
            if (guildProperty.Value.ValueKind != JsonValueKind.Object)
                continue;

            var guildObject = guildProperty.Value;
            var record = new GuildRecord();

            if (guildObject.TryGetProperty("Type", out var typeElement)
                && typeElement.ValueKind == JsonValueKind.String)
            {
                record.Type = typeElement.GetString() ?? string.Empty;
            }

            if (guildObject.TryGetProperty("Logo", out var logoElement)
                && logoElement.ValueKind == JsonValueKind.String)
            {
                record.Logo = logoElement.GetString() ?? string.Empty;
            }

            if (guildObject.TryGetProperty("MiracleList", out var miracleListElement))
            {
                record.MiracleList = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
                    miracleListElement.GetRawText(),
                    _jsonOptions) ?? new Dictionary<string, List<string>>();
            }

            if (guildObject.TryGetProperty("DenominationalMiracle", out var denominationalElement))
            {
                record.DenominationalMiracle = JsonSerializer.Deserialize<GuildMiracleReference>(
                    denominationalElement.GetRawText(),
                    _jsonOptions);
            }

            if (guildObject.TryGetProperty("DenominationalMiracleNote", out var denominationalNoteElement)
                && denominationalNoteElement.ValueKind == JsonValueKind.String)
            {
                record.DenominationalMiracleNote = denominationalNoteElement.GetString() ?? string.Empty;
            }

            map[guildProperty.Name] = record;
        }

        _miracleSearchCache = map;
        return _miracleSearchCache;
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

        var availability = record.Availability;
        if (availability?.Rules is { Count: > 0 })
        {
            var fromRules = BuildAlignmentRuleFromRules(availability.Rules);
            if (fromRules != null)
                return fromRules;
        }

        return null;
    }

    private static AlignmentRule? BuildAlignmentRuleFromRules(
        IEnumerable<RuleClause> rules)
    {
        var items = (rules ?? Array.Empty<RuleClause>())
            .Where(r => r != null && r.IsValid)
            .ToList();

        if (items.Count == 0)
            return null;

        HashSet<OrderAxis>? orderConstraint = null;
        HashSet<MoralAxis>? moralConstraint = null;

        foreach (var rule in items)
        {
            var normalizedField = NormalizeRuleField(rule.Field);
            if (normalizedField != "alignmentorder" && normalizedField != "alignmentmoral")
                continue;

            if (rule.Operator != RuleComparisonOp.In)
                return null;

            if (normalizedField == "alignmentorder")
            {
                MergeConstraint(ref orderConstraint, ParseOrders(rule.Value));
            }
            else
            {
                MergeConstraint(ref moralConstraint, ParseMorals(rule.Value));
            }
        }

        var orders = orderConstraint ?? new HashSet<OrderAxis>();
        var morals = moralConstraint ?? new HashSet<MoralAxis>();
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

    private static string NormalizeRuleField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static void MergeConstraint<TEnum>(ref HashSet<TEnum>? existing, HashSet<TEnum> incoming)
        where TEnum : struct, Enum
    {
        if (incoming.Count == 0)
            return;

        if (existing == null)
        {
            existing = incoming;
            return;
        }

        existing.IntersectWith(incoming);
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
    public string Logo { get; set; } = "";
    public string PreRequisites { get; set; } = "";
    public string Restrictions { get; set; } = "";
    public string Ethos { get; set; } = "";
    public string Background { get; set; } = "";

    public GuildBenefits Benefits { get; set; } = new();

    [JsonPropertyName("alignmentRule")]
    public AlignmentRule? AlignmentRule { get; set; }

    public Dictionary<string, List<string>> MiracleList { get; set; } = new();
    public GuildMiracleReference? DenominationalMiracle { get; set; }
    public string DenominationalMiracleNote { get; set; } = string.Empty;

    public GuildAvailability Availability { get; set; } = new();
}

[JsonConverter(typeof(GuildMiracleReferenceConverter))]
public sealed class GuildMiracleReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;
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
    public List<RuleClause> Rules { get; set; } = new();
    public string? RequiredGuild { get; set; }
}

public sealed class GuildMiracleReferenceConverter : JsonConverter<GuildMiracleReference>
{
    public override GuildMiracleReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new GuildMiracleReference { Ref = reader.GetString() ?? string.Empty };

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Unexpected token {reader.TokenType} when parsing guild miracle reference.");

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var reference = new GuildMiracleReference();

        if (root.TryGetProperty("$ref", out var refEl))
            reference.Ref = refEl.GetString() ?? string.Empty;
        else if (root.TryGetProperty("ref", out var compatRef))
            reference.Ref = compatRef.GetString() ?? string.Empty;
        else if (root.TryGetProperty("Name", out var nameEl))
            reference.Ref = nameEl.GetString() ?? string.Empty;

        return reference;
    }

    public override void Write(Utf8JsonWriter writer, GuildMiracleReference value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("$ref", value?.Ref ?? string.Empty);
        writer.WriteEndObject();
    }
}
