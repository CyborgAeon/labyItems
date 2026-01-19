using System.Text.Json;
using Android.App;

namespace labyItems.Services;

public static class SpecialisationService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static Dictionary<string, SpecialisationRecord>? _cache;

    public static async Task<Dictionary<string, SpecialisationRecord>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        await using var stream = await FileSystem.OpenAppPackageFileAsync("specialisation/specialisation.json");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();

        var dict = JsonSerializer.Deserialize<Dictionary<string, SpecialisationRecord>>(json, _jsonOptions)
                   ?? new Dictionary<string, SpecialisationRecord>(StringComparer.OrdinalIgnoreCase);

        _cache = new Dictionary<string, SpecialisationRecord>(dict, StringComparer.OrdinalIgnoreCase);
        return _cache;
    }
}

public sealed class SpecialisationRecord
{
    public List<string>? Abilities { get; set; }
    public PowerListRecord? PowerList { get; set; }

    // For tables like ElfColourAbilities
    public Dictionary<string, ColourAbilityRecord>? ColourAbilities { get; set; }
}

public sealed class ColourAbilityRecord
{
    public Dictionary<string, List<string>>? Levels { get; set; }
}

public sealed class PowerListRecord
{
    public int Max { get; set; }
    public List<string>? Requirements { get; set; }
}
