using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models.Characters;

[JsonConverter(typeof(AbilityDefinitionConverter))]
public sealed class AbilityDefinition
{
    public string? Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BattleboardNameOverride { get; set; }
    public string? UpdateKey { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Effect { get; set; }
    public string? Lore { get; set; }
    public string? Source { get; set; }
    public int? Count { get; set; }
    public List<int>? Amount { get; set; }
    public string? Frequency { get; set; }
    public string? OverwriteKey { get; set; }
    public List<string>? PreReqs { get; set; }
    public List<string>? GuildOverrides { get; set; }
    public AbilityCustomisation? Customisation { get; set; }
}

public sealed class AbilityCustomisation
{
    public string? OptionEnum { get; set; }
    public bool CustomValuesPermitted { get; set; }
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
                Amount = ReadAmount(el)
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
        if (value.Amount is { Count: > 0 })
        {
            writer.WritePropertyName("Amount");
            JsonSerializer.Serialize(writer, value.Amount, options);
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
        writer.WriteEndObject();
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
}
