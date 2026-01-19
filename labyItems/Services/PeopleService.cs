using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class PeopleService
{

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static Dictionary<string, PeopleRecord>? _cache;

    public static async Task<Dictionary<string, PeopleRecord>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("people/people.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();

        _cache = JsonSerializer.Deserialize<Dictionary<string, PeopleRecord>>(json)
                 ?? new Dictionary<string, PeopleRecord>();

        return _cache;
    }
}
public sealed class PeopleRecord
{
    public string PeopleType { get; set; } = "";
    public string Description { get; set; } = "";

    [JsonPropertyName("levelledAbilities")]
    public Dictionary<string, List<string>> LevelledAbilities { get; set; } = new();

    public string? AdditionalInfo { get; set; }

    public PeopleSubtypeRecord? Subtype { get; set; }

    // legacy fields you may still have in older JSON
    [JsonPropertyName("Buy-as")]
    public string? BuyAs { get; set; }
}

public sealed class PeopleSubtypeRecord
{
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";

    // e.g. "SingleRequired"
    public string SelectionMode { get; set; } = "SingleOptional";

    // e.g. "Enum:ElfColours"
    public string OptionsSource { get; set; } = "";

    // e.g. "ElfColourAbilities"
    public string AbilityMapKey { get; set; } = "";
}
