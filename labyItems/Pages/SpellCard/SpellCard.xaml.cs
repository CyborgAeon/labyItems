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

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (Navigation?.ModalStack?.Count > 0)
        {
            await Navigation.PopModalAsync();
            return;
        }

        if (Navigation?.NavigationStack?.Count > 1)
            await Navigation.PopAsync();
    }
}
