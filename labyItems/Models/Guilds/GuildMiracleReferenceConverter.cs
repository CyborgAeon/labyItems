using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models.Guilds;

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
