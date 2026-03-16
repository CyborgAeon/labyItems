using System.Text.Json;
using labyItems.Models.Characters;
using labyItems.Models.Rules;

namespace labyItems.Services;

public interface IMultiPathDefinition<TAvailabilityOption, TLevelAbility, TSystemEffect>
{
    string DisplayName { get; }
    int MaxLevel { get; }
    Dictionary<string, List<TLevelAbility>> Levels { get; }
    List<TAvailabilityOption> AvailabilityOptions { get; }
    Dictionary<string, List<TSystemEffect>> SystemEffectsByLevel { get; }
    string? RequiresBracketPure { get; }
}

public interface IMultiPathAvailabilityOption
{
    string Source { get; }
    string Display { get; }
    List<RuleClause> Rules { get; }
    Dictionary<string, int> CostsByLevel { get; }
}

public interface IMultiPathLevelAbility
{
    string Name { get; }
    string AbilityRef { get; }
    string Type { get; }
    string Effect { get; }
    int? Count { get; }
    AbilityCountProgression? Progression { get; }
    List<string>? AsPerAbilityRefs { get; }
    List<string>? PreReqs { get; }
    List<string>? ChoiceSetRefs { get; }
}

public interface IMultiPathLifeScaleReference
{
    string File { get; }
    string Class { get; }
    int Level { get; }
}

public interface IMultiPathSystemEffect<TLifeScaleReference>
    where TLifeScaleReference : class, IMultiPathLifeScaleReference
{
    string EffectType { get; }
    string Display { get; }
    JsonElement Value { get; }
    TLifeScaleReference? LifeScaleReference { get; }
    List<RuleClause> Conditions { get; }
    string SourceCategory { get; }
    List<string> LinkedAbilityRefs { get; }
}
