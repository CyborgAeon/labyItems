using System.Text.Json.Serialization;

namespace labyItems.Models.Rules;

public enum RuleComparisonOp
{
    In,
    NotIn
}

public sealed class RuleClause
{
    public string Field { get; set; } = string.Empty;
    public RuleComparisonOp Operator { get; set; } = RuleComparisonOp.In;
    public List<string> Value { get; set; } = new();

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Field);
}
