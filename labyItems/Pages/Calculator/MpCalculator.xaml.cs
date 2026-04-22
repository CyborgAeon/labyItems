namespace labyItems.Pages.Calculator;

public partial class MpCalculator : ContentPage
{
    private readonly Func<MpSubmissionPayload, Task>? _onCharacterItemSubmit;

    partial void ApplyPlatformTabLayoutTweaks();

    public MpCalculator(Func<MpSubmissionPayload, Task>? onCharacterItemSubmit = null)
    {
        InitializeComponent();
        _onCharacterItemSubmit = onCharacterItemSubmit;
    }

    private async void OnBasicTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new MpBasicPage
        {
            CharacterItemSubmitHandler = _onCharacterItemSubmit
        });
    }

    private async void OnCrewTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new MpCrewPage
        {
            CharacterItemSubmitHandler = _onCharacterItemSubmit
        });
    }

    private async void OnThemeDayTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new MpThemedayPage
        {
            CharacterItemSubmitHandler = _onCharacterItemSubmit
        });
    }
}
