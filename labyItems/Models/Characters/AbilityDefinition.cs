using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models.Characters;

[JsonConverter(typeof(AbilityDefinitionConverter))]
public sealed class AbilityDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Effect { get; set; }
    public string? Source { get; set; }
    public int? Count { get; set; }
    public string? Frequency { get; set; }
    public string? OverwriteKey { get; set; }
    public List<string>? PreReqs { get; set; }
    public List<string>? GuildOverrides { get; set; }
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
                Name = el.TryGetProperty("Name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty,
                Type = el.TryGetProperty("Type", out var typeEl) ? typeEl.GetString() ?? string.Empty : string.Empty,
                Effect = el.TryGetProperty("Effect", out var effectEl) ? effectEl.GetString() : null,
                Source = el.TryGetProperty("Source", out var sourceEl) ? sourceEl.GetString() : null,
                Frequency = ReadFrequency(el),
                OverwriteKey = el.TryGetProperty("OverwriteKey", out var overwriteEl) ? overwriteEl.GetString() : null
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
                def.PreReqs = preReqEl
                    .EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .ToList();
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

    public override void Write(Utf8JsonWriter writer, AbilityDefinition value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Name", value.Name);
        if (!string.IsNullOrWhiteSpace(value.Type)) writer.WriteString("Type", value.Type);
        if (!string.IsNullOrWhiteSpace(value.Effect)) writer.WriteString("Effect", value.Effect);
        if (!string.IsNullOrWhiteSpace(value.Source)) writer.WriteString("Source", value.Source);
        if (value.Count.HasValue) writer.WriteNumber("Count", value.Count.Value);
        if (!string.IsNullOrWhiteSpace(value.Frequency)) writer.WriteString("Frequency", value.Frequency);
        if (!string.IsNullOrWhiteSpace(value.OverwriteKey)) writer.WriteString("OverwriteKey", value.OverwriteKey);
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
}
