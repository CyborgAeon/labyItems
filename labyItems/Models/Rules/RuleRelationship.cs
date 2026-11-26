using System.Text.Json.Serialization;

namespace labyItems.Models.Rules;

[JsonConverter(typeof(JsonStringEnumConverter<RuleRelationship>))]
public enum RuleRelationship
{
    References,
    Inherits,
    Modifies,
    Requires,
    ComparableTo,
    Terminology
}
