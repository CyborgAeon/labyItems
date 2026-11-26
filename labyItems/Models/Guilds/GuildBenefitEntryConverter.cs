using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Characters;

namespace labyItems.Models.Guilds;

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
            case JsonValueKind.Object when el.TryGetProperty("Id", out _) || el.TryGetProperty("Kind", out _):
                var node = GuildGrantNode.Parse(el, options);
                return new GuildBenefitEntry { Node = node, Ability = node.Ability };
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
