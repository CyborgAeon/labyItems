using labyItems.Services;

namespace labyItems.Pages.SpellCard;

public partial class SpellCard : ContentPage
{
    public SpellCard()
    {
        InitializeComponent();
    }

    public SpellCard(SpellService.SpellRaw spell)
        : this()
    {
        SpellDetails.Spell = spell;
        Title = spell?.isAdvanced == true || spell?.IsAdvancedCompat == true ? "Advanced Grimoire" : "Wizard Grimoire";
        DetailHeader.HeaderText = Title;
    }
}
