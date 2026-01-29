using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using SQLite;

namespace labyItems.Services;

public static class ClassService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new GuildOverrideRulesConverter() }
    };

    private static Dictionary<string, CharacterClassRecord>? _cache;

    public static Task<Dictionary<string, CharacterClassRecord>> GetAllAsync()
    {
        if (_cache != null) return Task.FromResult(_cache);

        try
        {
            using var conn = ServiceHelper.OpenReadOnlyConnection();
            var rows = conn.Query<ClassRow>("SELECT name, data_json FROM classes ORDER BY name;");
            var dict = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.name))
                    continue;

                var record = string.IsNullOrWhiteSpace(row.data_json)
                    ? new CharacterClassRecord()
                    : (JsonSerializer.Deserialize<CharacterClassRecord>(row.data_json, _jsonOptions) ?? new CharacterClassRecord());

                dict[row.name] = record;
            }

            _cache = dict;
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get classes", ex);
            throw;
        }

        return Task.FromResult(_cache);
    }

    private sealed class ClassRow
    {
        public string name { get; set; } = string.Empty;
        public string? data_json { get; set; }
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
