using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models.Guilds;

public sealed class GuildCityBenefitConverter : JsonConverter<GuildCityBenefit>
{
    public override GuildCityBenefit Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new GuildCityBenefit
            {
                Name = reader.GetString() ?? string.Empty
            };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Unexpected token {reader.TokenType} when parsing city benefit.");

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var benefit = new GuildCityBenefit();
        if (TryGetPropertyInsensitive(root, "Name", out var nameEl)
            && nameEl.ValueKind == JsonValueKind.String)
        {
            benefit.Name = nameEl.GetString() ?? string.Empty;
        }

        var effects = new List<string>();
        if (TryGetPropertyInsensitive(root, "Effects", out var effectsEl))
            AddEffects(effectsEl, effects);
        else if (TryGetPropertyInsensitive(root, "Effect", out var effectEl))
            AddEffects(effectEl, effects);

        benefit.Effects = effects;
        return benefit;
    }

    public override void Write(Utf8JsonWriter writer, GuildCityBenefit value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Name", value?.Name ?? string.Empty);
        writer.WritePropertyName("Effects");
        writer.WriteStartArray();
        foreach (var effect in value?.Effects ?? new List<string>())
            writer.WriteStringValue(effect);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void AddEffects(JsonElement value, ICollection<string> target)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                AddEffects(item, target);
            return;
        }

        if (value.ValueKind != JsonValueKind.String)
            return;

        var text = (value.GetString() ?? string.Empty).Trim();
        if (text.Length > 0)
            target.Add(text);
    }

    private static bool TryGetPropertyInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
