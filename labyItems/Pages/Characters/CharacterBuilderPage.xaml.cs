namespace labyItems.Pages;

public partial class CharacterBuilderPage : ContentPage
{
    private readonly CharacterBuilderViewModel _vm;

    public CharacterBuilderPage()
    {
        InitializeComponent();
        _vm = new CharacterBuilderViewModel();
        BindingContext = _vm;
        _vm.ContinueRequested += OnContinueRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private async Task OnContinueRequested(LifeScaleSelection selection)
    {
        if (Draft.)
            var levelNumber = selection.LevelIndex + 1;
        var lifescale = $"{selection.Level.Life}/{selection.Level.Spirit}";
        await DisplayAlert("Continue", $"{selection.Entry.Race} {selection.Entry.ClassName} - Level {levelNumber}: {lifescale}", "OK");
    }

    private async void OnRaceInfoClicked(object sender, EventArgs e)
    {
        if (_vm.SelectedRace is { } race)
        {
            var desc = _vm.GetRaceDescription(race.Name);
            await DisplayAlert(race.Name, desc, "OK");
        }
        else
        {
            await DisplayAlert("Race", "Select a race first.", "OK");
        }
    }

    private async void OnClassInfoClicked(object sender, EventArgs e)
    {
        if (_vm.SelectedClass is { } cls)
        {
            var desc = _vm.GetClassDescription(cls.Name);
            await DisplayAlert(cls.Name, desc, "OK");
        }
        else
        {
            await DisplayAlert("Class", "Select a class first.", "OK");
        }
    }

    private void OnClearRaceClicked(object sender, EventArgs e)
    {
        _vm.SelectedRace = null;
    }

    private void OnClearClassClicked(object sender, EventArgs e)
    {
        _vm.SelectedClass = null;
    }
}
