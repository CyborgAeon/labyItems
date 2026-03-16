using System.Collections.ObjectModel;
using labyItems.Models.Characters;

namespace labyItems.Services.Specialisations;

public enum ChoiceMode
{
    Single,
    Multi,
    MappedSingle,
    Lookup,
    Passive
}

public sealed class SpecialisationDefinition
{
    public string Key { get; init; } = string.Empty;
    public IReadOnlyList<SpecialisationChoiceSet> ChoiceSets { get; init; } = Array.Empty<SpecialisationChoiceSet>();
    public IReadOnlyList<AbilityGrant> PassiveGrants { get; init; } = Array.Empty<AbilityGrant>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}

public sealed class SpecialisationChoiceSet
{
    public string Id { get; init; } = string.Empty;
    public string DefinitionKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public bool Required { get; init; }
    public IReadOnlyList<int> Levels { get; init; } = Array.Empty<int>();
    public ChoiceMode Mode { get; init; } = ChoiceMode.Single;
    public IReadOnlyList<ChoiceOption> Options { get; init; } = Array.Empty<ChoiceOption>();
    public IReadOnlyList<string> StrategyIds { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}

public sealed class ChoiceOption
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<AbilityGrant> Grants { get; init; } = Array.Empty<AbilityGrant>();
    public OptionCustomisation? Customisation { get; init; }
    public OptionEffects Effects { get; init; } = new();
    public Restrictions Restrictions { get; init; } = new();
    public IReadOnlyList<string> StrategyIds { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}

public sealed class AbilityGrant
{
    public int? Level { get; init; }
    public AbilityDefinition Ability { get; init; } = new();
    public IReadOnlyList<string> StrategyIds { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}

public sealed class OptionCustomisation
{
    public string OptionEnum { get; init; } = string.Empty;
    public bool CustomValuesPermitted { get; init; }
}

public sealed class OptionEffects
{
    public string LifeScaleOverride { get; init; } = string.Empty;
    public string ArmourAvailabilityOverride { get; init; } = string.Empty;
    public IReadOnlyList<string> ColourChoiceOverride { get; init; } = Array.Empty<string>();
    public GuildOverrideRules? GuildOverrides { get; init; }
    public IReadOnlyList<string> HedgeOrCircle { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed class Restrictions
{
    public IReadOnlyList<string> ClassRestriction { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AlignmentRestriction { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RaceRestriction { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PeopleType { get; init; } = Array.Empty<string>();

    public bool HasRestrictions =>
        ClassRestriction.Count > 0
        || AlignmentRestriction.Count > 0
        || RaceRestriction.Count > 0
        || PeopleType.Count > 0;
}

public sealed class SpecialisationIndex
{
    public IReadOnlyDictionary<string, SpecialisationDefinition> Definitions { get; init; } =
        new ReadOnlyDictionary<string, SpecialisationDefinition>(new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, AbilityDefinition> AbilityReferences { get; init; } =
        new ReadOnlyDictionary<string, AbilityDefinition>(new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, SpecialisationChoiceSet> ChoiceSetTemplates { get; init; } =
        new ReadOnlyDictionary<string, SpecialisationChoiceSet>(new Dictionary<string, SpecialisationChoiceSet>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyList<SpecialisationInjectionRule> InjectionRules { get; init; } = Array.Empty<SpecialisationInjectionRule>();
}

public sealed class NormalizedRaceSubtype
{
    public string Race { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string AbilityMapKey { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> StrategyIds { get; init; } = Array.Empty<string>();
}

public sealed class SpecialisationInjectionRule
{
    public string Id { get; init; } = string.Empty;
    public InjectionRuleConditions Conditions { get; init; } = new();
    public InjectionRuleSection Section { get; init; } = new();
    public IReadOnlyList<string> StrategyIds { get; init; } = Array.Empty<string>();
}

public sealed class InjectionRuleConditions
{
    public string Race { get; init; } = string.Empty;
    public string Subtype { get; init; } = string.Empty;
    public string Class { get; init; } = string.Empty;
    public IReadOnlyList<string> ClassIn { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ClassNotIn { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PeopleTypeIn { get; init; } = Array.Empty<string>();
}

public sealed class InjectionRuleSection
{
    public string SectionType { get; init; } = "Mapped";
    public string DefinitionKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string DetailKey { get; init; } = string.Empty;
    public string SectionId { get; init; } = string.Empty;
    public bool? Required { get; init; }
    public bool RequiredWhenClassHasPowerBase { get; init; }
    public IReadOnlyList<string> OptionalClassIn { get; init; } = Array.Empty<string>();
    public IReadOnlyList<int> Levels { get; init; } = Array.Empty<int>();
    public int? InsertIndex { get; init; }
    public InjectionOptionFilter? OptionFilter { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}

public sealed class InjectionOptionFilter
{
    public string SourceDefinitionKey { get; init; } = string.Empty;
    public string SourceOption { get; init; } = string.Empty;
    public string EffectListField { get; init; } = string.Empty;
}
