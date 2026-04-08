using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models.Characters;

[JsonConverter(typeof(AbilityDefinitionConverter))]
public sealed class AbilityDefinition
{
    public string? Key { get; set; }
    public string? AbilityRef { get; set; }
    public string? GrantId { get; set; }
    public string? GrantType { get; set; }
    public string? Duration { get; set; }
    public GuildGrantOverrides? Overrides { get; set; }
    public string? UpgradeGrantRef { get; set; }
    public AbilityDefinition? ReplaceWith { get; set; }
    public GuildGrantModify? Modify { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BattleboardNameOverride { get; set; }
    public string? UpdateKey { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Effect { get; set; }
    public string? Lore { get; set; }
    public string? Source { get; set; }
    public int? Count { get; set; }
    public AbilityCountProgression? Progression { get; set; }
    public List<int>? Amount { get; set; }
    public List<string>? AsPer { get; set; }
    public string? Frequency { get; set; }
    public string? OverwriteKey { get; set; }
    public List<string>? PreReqs { get; set; }
    public List<string>? GuildOverrides { get; set; }
    public string? ChoiceSetRef { get; set; }
    public List<string>? ChoiceSetRefs { get; set; }
    public AbilityCustomisation? Customisation { get; set; }
    public List<AbilitySystemEffect>? SystemEffects { get; set; }
}

public sealed class GuildGrantOverrides
{
    public string? DisplayName { get; set; }
    public string? Verbal { get; set; }
    public string? Effect { get; set; }
    public string? Source { get; set; }
    public string? GrantType { get; set; }
    public int? Count { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
}

public sealed class GuildGrantModify
{
    public int? CountDelta { get; set; }
}

public sealed class AbilityCustomisation
{
    public string? OptionEnum { get; set; }
    public bool CustomValuesPermitted { get; set; }
}

public sealed class AbilitySystemEffect
{
    public string EffectType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ResistanceType { get; set; }
    public int? Level { get; set; }
    public string? ImmunityName { get; set; }
}

public sealed class AbilityCountProgression
{
    public int Amount { get; set; } = 1;
    public int PerLevels { get; set; } = 1;
    public int? Minimum { get; set; }
    public int? Maximum { get; set; }

    public int ResolveCount(int achievedLevel)
    {
        var levels = Math.Max(0, achievedLevel);
        var amount = Math.Max(0, Amount);
        var perLevels = Math.Max(1, PerLevels);

        var resolved = (int)Math.Floor((double)(levels * amount) / perLevels);
        if (Minimum.HasValue)
            resolved = Math.Max(resolved, Minimum.Value);
        if (Maximum.HasValue)
            resolved = Math.Min(resolved, Maximum.Value);

        return Math.Max(0, resolved);
    }
}

public sealed class AbilityDefinitionConverter : JsonConverter<AbilityDefinition>
{
    public override AbilityDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var name = reader.GetString() ?? string.Empty;
            return new AbilityDefinition { Name = name };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var el = doc.RootElement;
            var def = new AbilityDefinition
            {
                Key = el.TryGetProperty("Key", out var keyEl) ? keyEl.GetString() : null,
                AbilityRef = ReadAbilityRef(el),
                GrantId = ReadStringProperty(el, "GrantId"),
                GrantType = ReadStringProperty(el, "GrantType"),
                Duration = ReadStringProperty(el, "Duration"),
                Overrides = ReadGrantOverrides(el),
                UpgradeGrantRef = ReadStringProperty(el, "UpgradeGrantRef"),
                ReplaceWith = ReadReplaceWith(el, options),
                Modify = ReadModify(el),
                Name = el.TryGetProperty("Name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty,
                BattleboardNameOverride = el.TryGetProperty("BattleboardNameOverride", out var battleNameEl)
                    ? battleNameEl.GetString()
                    : null,
                UpdateKey = el.TryGetProperty("UpdateKey", out var updateKeyEl)
                    ? updateKeyEl.GetString()
                    : null,
                Type = el.TryGetProperty("Type", out var typeEl) ? typeEl.GetString() ?? string.Empty : string.Empty,
                Effect = ReadEffectOrDescription(el),
                Lore = ReadLore(el),
                Source = el.TryGetProperty("Source", out var sourceEl) ? sourceEl.GetString() : null,
                Frequency = ReadFrequency(el),
                OverwriteKey = el.TryGetProperty("OverwriteKey", out var overwriteEl) ? overwriteEl.GetString() : null,
                Customisation = ReadCustomisation(el),
                Progression = ReadProgression(el),
                Amount = ReadAmount(el),
                AsPer = ReadAsPer(el),
                ChoiceSetRef = ReadStringProperty(el, "ChoiceSetRef"),
                ChoiceSetRefs = ReadChoiceSetRefs(el),
                SystemEffects = ReadSystemEffects(el)
            };

            if (el.TryGetProperty("Count", out var countEl))
            {
                if (countEl.ValueKind == JsonValueKind.Number && countEl.TryGetInt32(out var n))
                    def.Count = n;
                else if (countEl.ValueKind == JsonValueKind.String && int.TryParse(countEl.GetString(), out n))
                    def.Count = n;
            }

            if (el.TryGetProperty("PreReqs", out var preReqEl) && preReqEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var entry in preReqEl.EnumerateArray())
                {
                    var parsed = ParsePreReq(entry);
                    if (!string.IsNullOrWhiteSpace(parsed))
                        list.Add(parsed!);
                }
                def.PreReqs = list;
            }

            if (el.TryGetProperty("GuildOverrides", out var guildEl) && guildEl.ValueKind == JsonValueKind.Array)
            {
                def.GuildOverrides = guildEl
                    .EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .ToList();
            }

            if (string.IsNullOrWhiteSpace(def.GrantType) && !string.IsNullOrWhiteSpace(def.Type))
                def.GrantType = def.Type;
            if (string.IsNullOrWhiteSpace(def.Type) && !string.IsNullOrWhiteSpace(def.GrantType))
                def.Type = def.GrantType;

            return def;
        }

        throw new JsonException($"Unsupported ability definition token {reader.TokenType}");
    }

    private static AbilityCustomisation? ReadCustomisation(JsonElement el)
    {
        if (!el.TryGetProperty("Customisation", out var customEl)
            && !el.TryGetProperty("Customization", out customEl))
            return null;

        if (customEl.ValueKind != JsonValueKind.Object)
            return null;

        var custom = new AbilityCustomisation
        {
            OptionEnum = customEl.TryGetProperty("OptionEnum", out var optEl) && optEl.ValueKind == JsonValueKind.String
                ? optEl.GetString()
                : null,
            CustomValuesPermitted = ReadCustomValuesPermitted(customEl)
        };

        if (string.IsNullOrWhiteSpace(custom.OptionEnum) && custom.CustomValuesPermitted == false)
            return custom;

        return custom;
    }

    private static bool ReadCustomValuesPermitted(JsonElement customEl)
    {
        if (!customEl.TryGetProperty("CustomValuesPermitted", out var permEl))
            return false;

        return permEl.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(permEl.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static string? ReadStringProperty(JsonElement el, string propertyName)
    {
        if (!el.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
            return null;

        return prop.GetString();
    }

    private static List<string>? ReadAsPer(JsonElement el)
    {
        if (!TryGetPropertyCaseInsensitive(el, out var asPerElement, "AsPer", "asPer", "asper"))
            return null;

        if (asPerElement.ValueKind == JsonValueKind.String)
        {
            var single = (asPerElement.GetString() ?? string.Empty).Trim();
            return single.Length == 0 ? null : new List<string> { single };
        }

        if (asPerElement.ValueKind != JsonValueKind.Array)
            return null;

        var values = asPerElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => (item.GetString() ?? string.Empty).Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.Count == 0 ? null : values;
    }

    private static bool TryGetPropertyCaseInsensitive(
        JsonElement element,
        out JsonElement value,
        params string[] propertyNames)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out value))
                return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            foreach (var name in propertyNames)
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static GuildGrantOverrides? ReadGrantOverrides(JsonElement el)
    {
        if (!el.TryGetProperty("Overrides", out var overridesEl)
            || overridesEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GuildGrantOverrides>(overridesEl.GetRawText());
        }
        catch
        {
            return null;
        }
    }

    private static AbilityDefinition? ReadReplaceWith(JsonElement el, JsonSerializerOptions options)
    {
        if (!el.TryGetProperty("ReplaceWith", out var replaceEl))
            return null;

        if (replaceEl.ValueKind == JsonValueKind.String)
        {
            var raw = (replaceEl.GetString() ?? string.Empty).Trim();
            if (raw.Length == 0)
                return null;

            return new AbilityDefinition { AbilityRef = raw };
        }

        if (replaceEl.ValueKind != JsonValueKind.Object)
            return null;

        try
        {
            return JsonSerializer.Deserialize<AbilityDefinition>(replaceEl.GetRawText(), options);
        }
        catch
        {
            return null;
        }
    }

    private static GuildGrantModify? ReadModify(JsonElement el)
    {
        if (!el.TryGetProperty("Modify", out var modifyEl)
            || modifyEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var countDelta = TryReadInt(modifyEl, "CountDelta");
        if (!countDelta.HasValue)
            return null;

        return new GuildGrantModify { CountDelta = countDelta };
    }

    private static string? ParsePreReq(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.String)
            return el.GetString();

        if (el.ValueKind != JsonValueKind.Object)
            return null;

        if (el.TryGetProperty("Ability", out var abilityEl) && abilityEl.ValueKind == JsonValueKind.String)
            return $"Ability:{abilityEl.GetString()}";

        if (el.TryGetProperty("PeopleType", out var peopleEl) && peopleEl.ValueKind == JsonValueKind.String)
            return $"PeopleType:{peopleEl.GetString()}";

        if (el.TryGetProperty("Class", out var classEl) && classEl.ValueKind == JsonValueKind.String)
            return $"Class:{classEl.GetString()}";

        return null;
    }

    public override void Write(Utf8JsonWriter writer, AbilityDefinition value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (!string.IsNullOrWhiteSpace(value.Key))
            writer.WriteString("Key", value.Key);
        if (!string.IsNullOrWhiteSpace(value.AbilityRef))
            writer.WriteString("AbilityRef", value.AbilityRef);
        if (!string.IsNullOrWhiteSpace(value.GrantId))
            writer.WriteString("GrantId", value.GrantId);
        if (!string.IsNullOrWhiteSpace(value.GrantType))
            writer.WriteString("GrantType", value.GrantType);
        if (!string.IsNullOrWhiteSpace(value.Duration))
            writer.WriteString("Duration", value.Duration);
        if (value.Overrides != null)
        {
            writer.WritePropertyName("Overrides");
            JsonSerializer.Serialize(writer, value.Overrides, options);
        }
        if (!string.IsNullOrWhiteSpace(value.UpgradeGrantRef))
            writer.WriteString("UpgradeGrantRef", value.UpgradeGrantRef);
        if (value.ReplaceWith != null)
        {
            writer.WritePropertyName("ReplaceWith");
            JsonSerializer.Serialize(writer, value.ReplaceWith, options);
        }
        if (value.Modify != null)
        {
            writer.WritePropertyName("Modify");
            JsonSerializer.Serialize(writer, value.Modify, options);
        }
        writer.WriteString("Name", value.Name);
        if (!string.IsNullOrWhiteSpace(value.BattleboardNameOverride))
            writer.WriteString("BattleboardNameOverride", value.BattleboardNameOverride);
        if (!string.IsNullOrWhiteSpace(value.UpdateKey))
            writer.WriteString("UpdateKey", value.UpdateKey);
        if (!string.IsNullOrWhiteSpace(value.Type)) writer.WriteString("Type", value.Type);
        if (!string.IsNullOrWhiteSpace(value.Effect)) writer.WriteString("Effect", value.Effect);
        if (!string.IsNullOrWhiteSpace(value.Lore)) writer.WriteString("Lore", value.Lore);
        if (!string.IsNullOrWhiteSpace(value.Source)) writer.WriteString("Source", value.Source);
        if (value.Count.HasValue) writer.WriteNumber("Count", value.Count.Value);
        if (value.Progression != null)
        {
            writer.WritePropertyName("Progression");
            JsonSerializer.Serialize(writer, value.Progression, options);
        }
        if (value.Amount is { Count: > 0 })
        {
            writer.WritePropertyName("Amount");
            JsonSerializer.Serialize(writer, value.Amount, options);
        }
        if (value.AsPer is { Count: > 0 })
        {
            writer.WritePropertyName("AsPer");
            JsonSerializer.Serialize(writer, value.AsPer, options);
        }
        if (!string.IsNullOrWhiteSpace(value.Frequency)) writer.WriteString("Frequency", value.Frequency);
        if (!string.IsNullOrWhiteSpace(value.OverwriteKey)) writer.WriteString("OverwriteKey", value.OverwriteKey);
        if (value.Customisation != null)
        {
            writer.WritePropertyName("Customisation");
            writer.WriteStartObject();
            if (!string.IsNullOrWhiteSpace(value.Customisation.OptionEnum))
                writer.WriteString("OptionEnum", value.Customisation.OptionEnum);
            writer.WriteBoolean("CustomValuesPermitted", value.Customisation.CustomValuesPermitted);
            writer.WriteEndObject();
        }
        if (value.PreReqs is { Count: > 0 })
        {
            writer.WritePropertyName("PreReqs");
            JsonSerializer.Serialize(writer, value.PreReqs, options);
        }
        if (value.GuildOverrides is { Count: > 0 })
        {
            writer.WritePropertyName("GuildOverrides");
            JsonSerializer.Serialize(writer, value.GuildOverrides, options);
        }
        if (!string.IsNullOrWhiteSpace(value.ChoiceSetRef))
            writer.WriteString("ChoiceSetRef", value.ChoiceSetRef);
        if (value.ChoiceSetRefs is { Count: > 0 })
        {
            writer.WritePropertyName("ChoiceSetRefs");
            JsonSerializer.Serialize(writer, value.ChoiceSetRefs, options);
        }
        if (value.SystemEffects is { Count: > 0 })
        {
            writer.WritePropertyName("SystemEffects");
            JsonSerializer.Serialize(writer, value.SystemEffects, options);
        }
        writer.WriteEndObject();
    }

    private static string? ReadAbilityRef(JsonElement el)
    {
        if (el.TryGetProperty("AbilityRef", out var abilityRefEl) && abilityRefEl.ValueKind == JsonValueKind.String)
            return abilityRefEl.GetString();

        if (el.TryGetProperty("$ref", out var refEl) && refEl.ValueKind == JsonValueKind.String)
            return refEl.GetString();

        if (el.TryGetProperty("Ref", out var compatRefEl) && compatRefEl.ValueKind == JsonValueKind.String)
            return compatRefEl.GetString();

        return null;
    }

    private static string? ReadFrequency(JsonElement el)
    {
        if (!el.TryGetProperty("Frequency", out var freqEl))
            return null;

        return freqEl.ValueKind switch
        {
            JsonValueKind.String => freqEl.GetString(),
            JsonValueKind.Number => freqEl.GetRawText(),
            _ => null
        };
    }

    private static string? ReadEffectOrDescription(JsonElement el)
    {
        string? effect = null;
        if (el.TryGetProperty("Effect", out var effectEl) && effectEl.ValueKind == JsonValueKind.String)
            effect = effectEl.GetString();

        if (!string.IsNullOrWhiteSpace(effect))
            return effect;

        if (el.TryGetProperty("Description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
            return descEl.GetString();

        return effect;
    }

    private static string? ReadLore(JsonElement el)
    {
        if (el.TryGetProperty("Lore", out var loreEl) && loreEl.ValueKind == JsonValueKind.String)
            return loreEl.GetString();

        if (el.TryGetProperty("lore", out var loreLowerEl) && loreLowerEl.ValueKind == JsonValueKind.String)
            return loreLowerEl.GetString();

        return null;
    }

    private static List<int>? ReadAmount(JsonElement el)
    {
        if (!el.TryGetProperty("Amount", out var amtEl) || amtEl.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<int>();
        foreach (var item in amtEl.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var n))
                list.Add(n);
            else if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out n))
                list.Add(n);
        }

        return list.Count > 0 ? list : null;
    }

    private static AbilityCountProgression? ReadProgression(JsonElement el)
    {
        if (!el.TryGetProperty("Progression", out var progressionEl)
            || progressionEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var amount = TryReadInt(progressionEl, "Amount") ?? 1;
        var perLevels = TryReadInt(progressionEl, "PerLevels") ?? 1;
        var minimum = TryReadInt(progressionEl, "Minimum");
        var maximum = TryReadInt(progressionEl, "Maximum");

        if (amount <= 0 || perLevels <= 0)
            return null;

        return new AbilityCountProgression
        {
            Amount = amount,
            PerLevels = perLevels,
            Minimum = minimum,
            Maximum = maximum
        };
    }

    private static List<string>? ReadChoiceSetRefs(JsonElement el)
    {
        var list = new List<string>();
        if (el.TryGetProperty("ChoiceSetRefs", out var refsEl) && refsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in refsEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    continue;

                var value = (item.GetString() ?? string.Empty).Trim();
                if (value.Length > 0)
                    list.Add(value);
            }
        }

        if (el.TryGetProperty("ChoiceSetRef", out var refEl) && refEl.ValueKind == JsonValueKind.String)
        {
            var single = (refEl.GetString() ?? string.Empty).Trim();
            if (single.Length > 0 && !list.Contains(single, StringComparer.OrdinalIgnoreCase))
                list.Add(single);
        }

        return list.Count > 0 ? list : null;
    }

    private static List<AbilitySystemEffect>? ReadSystemEffects(JsonElement el)
    {
        if (!el.TryGetProperty("SystemEffects", out var effectsEl)
            && !el.TryGetProperty("systemEffects", out effectsEl))
        {
            return null;
        }

        if (effectsEl.ValueKind != JsonValueKind.Array)
            return null;

        try
        {
            var parsed = JsonSerializer.Deserialize<List<AbilitySystemEffect>>(effectsEl.GetRawText());
            return parsed is { Count: > 0 } ? parsed : null;
        }
        catch
        {
            return null;
        }
    }

    private static int? TryReadInt(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }
}
