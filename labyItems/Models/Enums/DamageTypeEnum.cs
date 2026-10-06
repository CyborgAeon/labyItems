using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models.Enums;

public enum DamageTypeEnum
{
    Unknown = 0,
    Acid,
    Air,
    BlackMana,
    Cold,
    Dessication,
    Fire,
    FleshAndBlood,
    InvoluntaryShapeShifting,
    Light,
    Lightning,
    Magic,
    Neuronic,
    Pain,
    Physical,
    Poison,
    Spiritual,
    Water
}

public static class DamageTypeEnumExtensions
{
    public static string ToDisplayText(this DamageTypeEnum value) => value switch
    {
        DamageTypeEnum.BlackMana => "Black Mana",
        DamageTypeEnum.FleshAndBlood => "Flesh and Blood",
        DamageTypeEnum.InvoluntaryShapeShifting => "Involuntary Shape-shifting",
        DamageTypeEnum.Unknown => string.Empty,
        _ => value.ToString()
    };
}

public sealed class SingleOrArrayDamageTypeConverter : JsonConverter<List<DamageTypeEnum>?>
{
    public override List<DamageTypeEnum>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return new List<DamageTypeEnum>();

        var values = new List<DamageTypeEnum>();
        if (reader.TokenType == JsonTokenType.String)
        {
            Add(values, reader.GetString());
            return values;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"Unexpected token {reader.TokenType} when parsing DamageType.");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
                Add(values, reader.GetString());
            else
                reader.Skip();
        }

        return values;
    }

    public override void Write(Utf8JsonWriter writer, List<DamageTypeEnum>? value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value ?? new List<DamageTypeEnum>())
            writer.WriteStringValue(item.ToDisplayText());
        writer.WriteEndArray();
    }

    private static void Add(ICollection<DamageTypeEnum> values, string? raw)
    {
        var key = new string((raw ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        if (Enum.TryParse<DamageTypeEnum>(key, true, out var parsed) && parsed != DamageTypeEnum.Unknown)
            values.Add(parsed);
    }
}
