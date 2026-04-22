using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardDashboardPage : ContentPage
{
    public NonStandardDashboardPage()
    {
        InitializeComponent();
    }

    private async void OnOpenClassClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new NonStandardClassCreatePage());

    private async void OnOpenClassTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(new NonStandardClassCreatePage());

    private async void OnOpenRaceClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.CharacterRace));

    private async void OnOpenRaceTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.CharacterRace));

    private async void OnOpenSpellClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.Spell));

    private async void OnOpenSpellTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.Spell));

    private async void OnOpenMiracleClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.Miracle));

    private async void OnOpenMiracleTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.Miracle));

    private async void OnOpenEvocationClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.Evocation));

    private async void OnOpenEvocationTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.Evocation));

    private async void OnOpenAbilityClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.Ability));

    private async void OnOpenAbilityTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(CreateLegacyPage(NonStandardEntityType.Ability));

    private static NonStandardLegacyCreatePage CreateLegacyPage(NonStandardEntityType entityType)
        => new()
        {
            FixedEntityTypeKey = entityType.ToString()
        };
}
