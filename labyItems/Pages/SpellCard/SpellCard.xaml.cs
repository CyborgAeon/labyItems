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
        Title = string.IsNullOrWhiteSpace(spell?.name) ? "Spell" : spell.name;
    }
}
