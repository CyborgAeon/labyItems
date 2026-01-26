using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class ClassService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new GuildOverrideRulesConverter() }
    };

    private static Dictionary<string, CharacterClassRecord>? _cache;

    public static async Task<Dictionary<string, CharacterClassRecord>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("people/classes.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();

        _cache = JsonSerializer.Deserialize<Dictionary<string, CharacterClassRecord>>(json, _jsonOptions)
                 ?? new Dictionary<string, CharacterClassRecord>();

        return _cache;
    }
}

public sealed class CharacterClassRecord
{
    public List<string> Brackets { get; set; } = new();
    public Dictionary<string, List<AbilityDefinition>> Levels { get; set; } = new();

    public ArmourAllowance? Armour { get; set; }

    [JsonPropertyName("Max AC")]
    public JsonElement MaxAC { get; set; }

    public List<string>? Powerbase { get; set; }

    public JsonElement PowerPerLevel { get; set; }

    public int? CasterLevel { get; set; }

    public List<PowerCalculation>? PowerCalculations { get; set; }

    [JsonPropertyName("Buy as")]
    public List<string>? BuyAs { get; set; }
    [JsonPropertyName("alignmentRule")]
    public AlignmentRule? AlignmentRule { get; set; }

    public GuildOverrideRules? GuildOverrides { get; set; }
}

public sealed class PowerCalculation
{
    public string PowerBase { get; set; } = "";
    public string Calculation { get; set; } = "";
}

public sealed class ArmourAllowance
{
    public List<string> Wearable { get; set; } = new();
    public List<string>? Restrictions { get; set; }
}
