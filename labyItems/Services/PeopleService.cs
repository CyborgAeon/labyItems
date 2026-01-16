using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class PeopleService
{
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

    [JsonPropertyName("Buy-as")]
    public string? BuyAs { get; set; }
}
