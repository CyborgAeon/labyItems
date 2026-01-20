using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Services;

public static class ClassService
{
    private static Dictionary<string, CharacterClassRecord>? _cache;

    public static async Task<Dictionary<string, CharacterClassRecord>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("people/classes.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();

        _cache = JsonSerializer.Deserialize<Dictionary<string, CharacterClassRecord>>(json)
                 ?? new Dictionary<string, CharacterClassRecord>();

        return _cache;
    }
}

public sealed class CharacterClassRecord
{
    public List<string> Brackets { get; set; } = new();
    public Dictionary<string, List<string>> Levels { get; set; } = new();

    [JsonPropertyName("Max AC")]
    public JsonElement MaxAC { get; set; }

    public List<string>? Powerbase { get; set; }

    public JsonElement PowerPerLevel { get; set; }

    [JsonPropertyName("Buy as")]
    public List<string>? BuyAs { get; set; }
    [JsonPropertyName("alignmentRule")]
    public AlignmentRule? AlignmentRule { get; set; }
}
