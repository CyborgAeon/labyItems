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
        Title = string.IsNullOrWhiteSpace(ability.SourceBook) ? "null source book" : ability.SourceBook;
        DetailHeader.HeaderText = Title;
    }

}
