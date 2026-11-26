using System.Text.Json.Serialization;

namespace labyItems.Models.Rules;

[JsonConverter(typeof(JsonStringEnumConverter<RuleCatalogueKind>))]
public enum RuleCatalogueKind
{
    Ability,
    Miracle,
    Spell,
    Evocation,
    Neuronic,
    ScoutSkill,
    Terminology,
    SystemRule
}
