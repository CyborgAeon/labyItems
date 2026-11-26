using System.Text.Json.Serialization;
using labyItems.Models.Rules;

namespace labyItems.Models.Characters;

[JsonConverter(typeof(AbilityDefinitionConverter))]
public sealed class AbilityDefinition
{
    public string? Key { get; set; }
    public string? AbilityRef { get; set; }
    public string? GrantId { get; set; }
    public string? GrantType { get; set; }
    public string? Duration { get; set; }
    public GuildGrantOverrides? Overrides { get; set; }
    public string? UpgradeGrantRef { get; set; }
    public AbilityDefinition? ReplaceWith { get; set; }
    public GuildGrantModify? Modify { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BattleboardNameOverride { get; set; }
    public string? UpdateKey { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Effect { get; set; }
    public string? Lore { get; set; }
    public string? Family { get; set; }
    public IReadOnlyList<RuleReference>? RuleReferences { get; set; }
    public string? Source { get; set; }
    public int? Count { get; set; }
    public AbilityCountProgression? Progression { get; set; }
    public List<int>? Amount { get; set; }
    public List<string>? AsPer { get; set; }
    public string? Frequency { get; set; }
    public string? OverwriteKey { get; set; }
    public List<string>? PreReqs { get; set; }
    public List<string>? GuildOverrides { get; set; }
    public string? ChoiceSetRef { get; set; }
    public List<string>? ChoiceSetRefs { get; set; }
    public AbilityCustomisation? Customisation { get; set; }
    public List<AbilitySystemEffect>? SystemEffects { get; set; }
}
