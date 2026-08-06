using labyItems.Services;

namespace labyItems.Pages.AbilityCard;

public partial class AbilityCard : ContentPage
{
    public AbilityCard()
    {
        InitializeComponent();
    }

    public AbilityCard(EvolutionService.AbilityResult ability)
        : this()
    {
        AbilityDetails.Ability = ability;
        Title = string.IsNullOrWhiteSpace(ability.Index) ? "Ability" : ability.Index;
    }

    public AbilityCard(ManuAbilityService.ManuAbilityEntry ability)
        : this(new EvolutionService.AbilityResult
        {
            Index = ability.name ?? string.Empty,
            Description = ability.description ?? string.Empty,
            Cost = ability.cost,
            Table = ability.table,
            AbilityRef = ability.abilityRef ?? string.Empty,
            Available = ability.availability ?? string.Empty,
            AvailabilityRules = ability.availabilityRules ?? Array.Empty<Models.Rules.RuleClause>(),
            SourceBook = ability.sourceBook ?? string.Empty,
            CanBuyMultiple = ability.canBuyMultiple,
            PreReqs = ability.preReqs ?? Array.Empty<string>(),
            ChoiceSetRefs = ability.choiceSetRefs ?? Array.Empty<string>(),
            MaxAvailable = null
        })
    {
    }
}
