using System.Text.Json.Serialization;

namespace labyItems.Models.Rules;

public enum RuleComparisonOp
{
    In,
    NotIn,
    Only
}

public sealed class RuleClause
{
    public string Field { get; set; } = string.Empty;
    public RuleComparisonOp Operator { get; set; } = RuleComparisonOp.In;
    public List<string> Value { get; set; } = new();
    public string? SpecialisationKey { get; set; }

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Field);
}
