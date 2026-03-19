using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Services.Specialisations;

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
        await NormalizeGuildBenefitsAsync(_cache);

        return _cache;
    }

    private static async Task NormalizeGuildBenefitsAsync(Dictionary<string, GuildRecord> records)
    {
        if (records == null || records.Count == 0)
            return;

        try
        {
            var index = await SpecialisationDefinitionRepository.GetIndexAsync();
            var abilityRefs = index?.AbilityReferences
                              ?? new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
            var choiceSetRefs = index?.ChoiceSetTemplates
                               ?? new Dictionary<string, SpecialisationChoiceSet>(StringComparer.OrdinalIgnoreCase);

            foreach (var record in records.Values)
            {
                record.Benefits ??= new GuildBenefits();
                record.Benefits.Basic = NormalizeBenefitTier(record.Benefits.Basic, abilityRefs, choiceSetRefs);
                record.Benefits.Intermediate = NormalizeBenefitTier(record.Benefits.Intermediate, abilityRefs, choiceSetRefs);
                record.Benefits.Advanced = NormalizeBenefitTier(record.Benefits.Advanced, abilityRefs, choiceSetRefs);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuildsService] Failed to normalize guild ability references: {ex.Message}");
        }
    }

    private static List<GuildBenefitEntry> NormalizeBenefitTier(
        IEnumerable<GuildBenefitEntry>? tier,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs,
        IReadOnlyDictionary<string, SpecialisationChoiceSet> choiceSetRefs)
    {
        var normalized = new List<GuildBenefitEntry>();
        foreach (var entry in tier ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            if (entry == null)
                continue;

            if (entry.Ability != null)
            {
                var ability = NormalizeAbility(entry.Ability, abilityRefs);
                if (ability.ChoiceSetRefs is { Count: > 0 })
                {
                    var prompt = CloneAbility(ability);
                    prompt.ChoiceSetRefs = null;
                    if (!string.IsNullOrWhiteSpace(prompt.Name))
                        normalized.Add(new GuildBenefitEntry { Ability = prompt });

                    var optionsEntry = BuildChoiceSetOptionEntry(ability.ChoiceSetRefs, abilityRefs, choiceSetRefs);
                    if (optionsEntry != null)
                        normalized.Add(optionsEntry);

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(ability.Name))
                    normalized.Add(new GuildBenefitEntry { Ability = ability });

                continue;
            }

            if (entry.Options == null || entry.Options.Count == 0)
                continue;

            var options = new List<GuildBenefitOption>();
            foreach (var option in entry.Options)
            {
                var abilities = (option?.Abilities ?? new List<AbilityDefinition>())
                    .Where(a => a != null)
                    .Select(a => NormalizeAbility(a, abilityRefs))
                    .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                    .ToList();

                if (abilities.Count == 0)
                    continue;

                options.Add(new GuildBenefitOption { Abilities = abilities });
            }

            if (options.Count > 0)
                normalized.Add(new GuildBenefitEntry { Options = options });
        }

        return normalized;
    }

    private static GuildBenefitEntry? BuildChoiceSetOptionEntry(
        IEnumerable<string> choiceSetRefs,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs,
        IReadOnlyDictionary<string, SpecialisationChoiceSet> choiceSets)
    {
        var options = new List<GuildBenefitOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var choiceSetRefRaw in choiceSetRefs ?? Enumerable.Empty<string>())
        {
            var choiceSetRef = (choiceSetRefRaw ?? string.Empty).Trim();
            if (choiceSetRef.Length == 0 || !choiceSets.TryGetValue(choiceSetRef, out var choiceSet))
                continue;

            foreach (var option in choiceSet.Options ?? Array.Empty<ChoiceOption>())
            {
                var abilities = new List<AbilityDefinition>();
                foreach (var grant in option.Grants ?? Array.Empty<AbilityGrant>())
                {
                    var ability = NormalizeAbility(grant.Ability ?? new AbilityDefinition(), abilityRefs);
                    if (!string.IsNullOrWhiteSpace(ability.Name))
                        abilities.Add(ability);
                }

                if (abilities.Count == 0)
                    continue;

                var signature = string.Join("|", abilities
                    .Select(a => !string.IsNullOrWhiteSpace(a.Key)
                        ? a.Key!.Trim()
                        : (a.Name ?? string.Empty).Trim().ToLowerInvariant()));

                if (!seen.Add(signature))
                    continue;

                options.Add(new GuildBenefitOption { Abilities = abilities });
            }
        }

        return options.Count == 0 ? null : new GuildBenefitEntry { Options = options };
    }

    private static AbilityDefinition NormalizeAbility(
        AbilityDefinition? raw,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs)
    {
        if (raw == null)
            return new AbilityDefinition();

        var resolved = TryResolveReferencedAbility(raw, abilityRefs);
        var ability = resolved != null ? CloneAbility(resolved) : new AbilityDefinition();

        if (!string.IsNullOrWhiteSpace(raw.Key))
            ability.Key = raw.Key;
        if (!string.IsNullOrWhiteSpace(raw.AbilityRef))
            ability.AbilityRef = raw.AbilityRef;
        if (!string.IsNullOrWhiteSpace(raw.Name))
            ability.Name = raw.Name;
        if (!string.IsNullOrWhiteSpace(raw.BattleboardNameOverride))
            ability.BattleboardNameOverride = raw.BattleboardNameOverride;
        if (!string.IsNullOrWhiteSpace(raw.UpdateKey))
            ability.UpdateKey = raw.UpdateKey;
        if (!string.IsNullOrWhiteSpace(raw.Type))
            ability.Type = raw.Type;
        if (!string.IsNullOrWhiteSpace(raw.Effect))
            ability.Effect = raw.Effect;
        if (!string.IsNullOrWhiteSpace(raw.Lore))
            ability.Lore = raw.Lore;
        if (!string.IsNullOrWhiteSpace(raw.Source))
            ability.Source = raw.Source;
        if (raw.Count.HasValue)
            ability.Count = raw.Count;
        if (raw.Progression != null)
            ability.Progression = CloneProgression(raw.Progression);
        if (raw.Amount is { Count: > 0 })
            ability.Amount = raw.Amount.ToList();
        if (!string.IsNullOrWhiteSpace(raw.Frequency))
            ability.Frequency = raw.Frequency;
        if (!string.IsNullOrWhiteSpace(raw.OverwriteKey))
            ability.OverwriteKey = raw.OverwriteKey;
        if (raw.PreReqs is { Count: > 0 })
            ability.PreReqs = raw.PreReqs.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (raw.GuildOverrides is { Count: > 0 })
            ability.GuildOverrides = raw.GuildOverrides.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (raw.Customisation != null)
            ability.Customisation = CloneCustomisation(raw.Customisation);
        if (raw.ChoiceSetRefs is { Count: > 0 })
            ability.ChoiceSetRefs = raw.ChoiceSetRefs.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        if (string.IsNullOrWhiteSpace(ability.AbilityRef))
            ability.AbilityRef = resolved?.Key;
        if (string.IsNullOrWhiteSpace(ability.Key))
            ability.Key = resolved?.Key ?? ability.AbilityRef;

        return ability;
    }

    private static AbilityDefinition? TryResolveReferencedAbility(
        AbilityDefinition raw,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs)
    {
        foreach (var tokenRaw in new[] { raw.AbilityRef, raw.Key })
        {
            var token = (tokenRaw ?? string.Empty).Trim();
            if (token.Length == 0)
                continue;

            if (abilityRefs.TryGetValue(token, out var found))
                return found;
        }

        return null;
    }

    private static AbilityDefinition CloneAbility(AbilityDefinition source)
    {
        return new AbilityDefinition
        {
            Key = source.Key,
            AbilityRef = source.AbilityRef,
            Name = source.Name ?? string.Empty,
            BattleboardNameOverride = source.BattleboardNameOverride,
            UpdateKey = source.UpdateKey,
            Type = source.Type ?? string.Empty,
            Effect = source.Effect,
            Lore = source.Lore,
            Source = source.Source,
            Count = source.Count,
            Progression = CloneProgression(source.Progression),
            Amount = source.Amount?.ToList(),
            Frequency = source.Frequency,
            OverwriteKey = source.OverwriteKey,
            PreReqs = source.PreReqs?.ToList(),
            GuildOverrides = source.GuildOverrides?.ToList(),
            Customisation = CloneCustomisation(source.Customisation),
            ChoiceSetRefs = source.ChoiceSetRefs?.ToList()
        };
    }

    private static AbilityCountProgression? CloneProgression(AbilityCountProgression? source)
    {
        if (source == null)
            return null;

        return new AbilityCountProgression
        {
            Amount = source.Amount,
            PerLevels = source.PerLevels,
            Minimum = source.Minimum,
            Maximum = source.Maximum
        };
    }

    private static AbilityCustomisation? CloneCustomisation(AbilityCustomisation? source)
    {
        if (source == null)
            return null;

        return new AbilityCustomisation
        {
            OptionEnum = source.OptionEnum,
            CustomValuesPermitted = source.CustomValuesPermitted
        };
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
