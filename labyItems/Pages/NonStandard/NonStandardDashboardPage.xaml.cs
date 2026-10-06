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
        => await Navigation.PushAsync(new NonStandardSpellCreatePage());

    private async void OnOpenSpellTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(new NonStandardSpellCreatePage());

    private async void OnOpenMiracleClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new NonStandardMiracleCreatePage());

    private async void OnOpenMiracleTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(new NonStandardMiracleCreatePage());

    private async void OnOpenEvocationClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new NonStandardEvocationCreatePage());

    private async void OnOpenEvocationTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(new NonStandardEvocationCreatePage());

    private async void OnOpenNeuronicClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new NonStandardNeuronicCreatePage());

    private async void OnOpenNeuronicTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(new NonStandardNeuronicCreatePage());

    private async void OnOpenAbilityClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new NonStandardAbilityCreatePage());

    private async void OnOpenAbilityTapped(object sender, TappedEventArgs e)
        => await Navigation.PushAsync(new NonStandardAbilityCreatePage());

    private static NonStandardLegacyCreatePage CreateLegacyPage(NonStandardEntityType entityType)
        => new()
        {
            FixedEntityTypeKey = entityType.ToString()
        };
}
