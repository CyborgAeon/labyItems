using labyItems.Services;

namespace labyItems.Models.Characters;

public abstract class MultiPathLevelAbility : IMultiPathLevelAbility
{
    private string? _name;
    private string? _type;
    private string? _effect;

    public string Name { get => _name ?? string.Empty; set => _name = value; }
    public string Type { get => _type ?? string.Empty; set => _type = value; }
    public string Effect { get => _effect ?? string.Empty; set => _effect = value; }
    public string AbilityRef { get; set; } = string.Empty;
    public int? Count { get; set; }
    public AbilityCountProgression? Progression { get; set; }
    public List<string>? AsPerAbilityRefs { get; set; }
    public List<string>? PreReqs { get; set; }
    public List<string>? ChoiceSetRefs { get; set; }

    internal void BindCanonicalText(AbilityDefinition definition)
    {
        _name ??= definition.Name;
        _type ??= definition.Type;
        _effect ??= definition.Effect;
    }
}
