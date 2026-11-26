using System.Text.Json.Serialization;
using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Models.Guilds;

public sealed class GuildRecord
{
    [JsonPropertyName("30kGift")]
    public bool Has30KGift { get; set; }
    public string Type { get; set; } = "";
    public string Logo { get; set; } = "";
    public string PreRequisites
    {
        get => Details.GetValueOrDefault(nameof(PreRequisites), string.Empty);
        set => Details[nameof(PreRequisites)] = value;
    }
    public string Restrictions
    {
        get => Details.GetValueOrDefault(nameof(Restrictions), string.Empty);
        set => Details[nameof(Restrictions)] = value;
    }
    public string Ethos
    {
        get => Details.GetValueOrDefault(nameof(Ethos), string.Empty);
        set => Details[nameof(Ethos)] = value;
    }
    public string Background
    {
        get => Details.GetValueOrDefault(nameof(Background), string.Empty);
        set => Details[nameof(Background)] = value;
    }

    public Dictionary<string, string> Details { get; set; } = new();
    public bool IsEternal { get; set; }
    public string? Source { get; set; }

    internal IReadOnlyDictionary<string, AbilityDefinition> AbilityReferences { get; set; }
        = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
    internal IReadOnlyDictionary<string, SpecialisationChoiceSet> ChoiceSetTemplates { get; set; }
        = new Dictionary<string, SpecialisationChoiceSet>(StringComparer.OrdinalIgnoreCase);

    internal GuildRecord CreateWorkingCopy()
    {
        var copy = (GuildRecord)MemberwiseClone();
        copy.Details = new Dictionary<string, string>(Details, StringComparer.OrdinalIgnoreCase);
        return copy;
    }

    private GuildBenefits _benefits = new();
    private Lazy<GuildBenefits>? _projectedBenefits;
    internal GuildBenefits RawBenefits => _benefits;

    public GuildBenefits Benefits
    {
        get => _projectedBenefits?.Value ?? _benefits;
        set => _benefits = value ?? new GuildBenefits();
    }

    internal void SetBenefitProjection(Func<GuildBenefits, GuildBenefits> project)
        => _projectedBenefits = new Lazy<GuildBenefits>(() => project(_benefits));

    [JsonPropertyName("City Benefits")]
    public List<GuildCityBenefit> CityBenefits { get; set; } = new();

    [JsonPropertyName("alignmentRule")]
    public AlignmentRule? AlignmentRule { get; set; }

    public GuildMiracleReference? DenominationalMiracle { get; set; }
    public string DenominationalMiracleNote { get; set; } = string.Empty;

    public GuildAvailability Availability { get; set; } = new();
}

