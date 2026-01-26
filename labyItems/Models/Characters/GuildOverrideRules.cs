using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models.Characters;

[JsonConverter(typeof(GuildOverrideRulesConverter))]
public sealed class GuildOverrideRules
{
    public bool IsCityBound { get; set; }
    public GuildOverrideChannel Social { get; set; } = new();
    public GuildOverrideChannel Professional { get; set; } = new();
    public GuildOverrideChannel Political { get; set; } = new();

    public static GuildOverrideRules Merge(params GuildOverrideRules?[] rules)
    {
        var result = new GuildOverrideRules();
        foreach (var r in rules)
        {
            if (r == null) continue;
            result.IsCityBound |= r.IsCityBound;
            MergeChannel(result.Social, r.Social);
            MergeChannel(result.Professional, r.Professional);
            MergeChannel(result.Political, r.Political);
        }

        return result;
    }

    private static void MergeChannel(GuildOverrideChannel target, GuildOverrideChannel? source)
    {
        if (source == null) return;

        if (source.CanJoin.HasValue)
            target.CanJoin = (target.CanJoin ?? true) && source.CanJoin.Value;

        if (!string.IsNullOrWhiteSpace(source.GuildPeople))
            target.GuildPeople = source.GuildPeople;

        if (source.ReplacedBy is { Count: > 0 })
        {
            foreach (var item in source.ReplacedBy.Where(x => !string.IsNullOrWhiteSpace(x)))
                target.ReplacedBy.Add(item);
        }
    }

    public static GuildOverrideRules? FromLegacyStrings(IEnumerable<string>? entries)
    {
        if (entries == null) return null;
        var rules = new GuildOverrideRules();
        var any = false;

        foreach (var raw in entries)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.Length == 0) continue;
            any = true;
            ApplyLegacyString(rules, text);
        }

        return any ? rules : null;
    }

    internal static void ApplyLegacyString(GuildOverrideRules target, string raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0) return;

        if (text.Equals("city bound", StringComparison.OrdinalIgnoreCase))
        {
            target.IsCityBound = true;
            return;
        }

        var parts = text.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return;

        var channel = parts[0].ToLowerInvariant() switch
        {
            "po" or "political" => target.Political,
            "pr" or "professional" => target.Professional,
            "so" or "social" => target.Social,
            _ => null
        };

        if (channel == null)
            return;

        var value = parts.Length > 1 ? parts[1] : string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            channel.CanJoin = false;
            return;
        }

        if (value.StartsWith("Type-", StringComparison.OrdinalIgnoreCase))
        {
            channel.GuildPeople = value.Substring("Type-".Length).Trim();
            channel.CanJoin = true;
            return;
        }

        channel.ReplacedBy.Add(value);
        channel.CanJoin = true;
    }
}

public sealed class GuildOverrideChannel
{
    public string GuildPeople { get; set; } = string.Empty;
    public List<string> ReplacedBy { get; set; } = new();
    public bool? CanJoin { get; set; }
}

public sealed class GuildOverrideRulesConverter : JsonConverter<GuildOverrideRules>
{
    public override GuildOverrideRules? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var rules = new GuildOverrideRules();

            if (root.TryGetProperty(nameof(GuildOverrideRules.IsCityBound), out var isCityBound)
                && (isCityBound.ValueKind == JsonValueKind.True || isCityBound.ValueKind == JsonValueKind.False))
            {
                rules.IsCityBound = isCityBound.GetBoolean();
            }

            if (root.TryGetProperty(nameof(GuildOverrideRules.Social), out var social))
                rules.Social = social.Deserialize<GuildOverrideChannel>(options) ?? new GuildOverrideChannel();

            if (root.TryGetProperty(nameof(GuildOverrideRules.Professional), out var professional))
                rules.Professional = professional.Deserialize<GuildOverrideChannel>(options) ?? new GuildOverrideChannel();

            if (root.TryGetProperty(nameof(GuildOverrideRules.Political), out var political))
                rules.Political = political.Deserialize<GuildOverrideChannel>(options) ?? new GuildOverrideChannel();

            return rules;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                    list.Add(reader.GetString() ?? string.Empty);
            }
            return GuildOverrideRules.FromLegacyStrings(list);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            return GuildOverrideRules.FromLegacyStrings(new[] { str ?? string.Empty });
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, GuildOverrideRules value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("IsCityBound", value.IsCityBound);
        writer.WritePropertyName("Social");
        JsonSerializer.Serialize(writer, value.Social, options);
        writer.WritePropertyName("Professional");
        JsonSerializer.Serialize(writer, value.Professional, options);
        writer.WritePropertyName("Political");
        JsonSerializer.Serialize(writer, value.Political, options);
        writer.WriteEndObject();
    }

    public static GuildOverrideRules? FromElement(JsonElement el, JsonSerializerOptions options)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            var rules = new GuildOverrideRules();

            if (el.TryGetProperty(nameof(GuildOverrideRules.IsCityBound), out var isCityBound)
                && (isCityBound.ValueKind == JsonValueKind.True || isCityBound.ValueKind == JsonValueKind.False))
            {
                rules.IsCityBound = isCityBound.GetBoolean();
            }

            if (el.TryGetProperty(nameof(GuildOverrideRules.Social), out var social))
                rules.Social = social.Deserialize<GuildOverrideChannel>(options) ?? new GuildOverrideChannel();

            if (el.TryGetProperty(nameof(GuildOverrideRules.Professional), out var professional))
                rules.Professional = professional.Deserialize<GuildOverrideChannel>(options) ?? new GuildOverrideChannel();

            if (el.TryGetProperty(nameof(GuildOverrideRules.Political), out var political))
                rules.Political = political.Deserialize<GuildOverrideChannel>(options) ?? new GuildOverrideChannel();

            return rules;
        }

        if (el.ValueKind == JsonValueKind.Array)
        {
            var list = el
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty)
                .ToList();
            return GuildOverrideRules.FromLegacyStrings(list);
        }

        if (el.ValueKind == JsonValueKind.String)
            return GuildOverrideRules.FromLegacyStrings(new[] { el.GetString() ?? string.Empty });

        return null;
    }
}
