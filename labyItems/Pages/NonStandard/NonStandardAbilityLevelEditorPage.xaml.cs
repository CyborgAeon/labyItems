namespace labyItems.Pages.NonStandard;

public partial class NonStandardAbilityLevelEditorPage : ContentPage
{
    private readonly NonStandardAbilityLevelEditorVm _vm;

    public NonStandardAbilityLevelEditorPage(NonStandardClassCreateVm ownerVm, AbilityLevelVm levelRow)
    {
        InitializeComponent();
        _vm = new NonStandardAbilityLevelEditorVm(ownerVm, levelRow);
        BindingContext = _vm;
    }

    private async void OnSearchAbilityClicked(object sender, EventArgs e)
    {
        try
        {
            await _vm.OwnerVm.AddAbilityToLevelAsync(Navigation, _vm.LevelRow);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search failed", ex.Message, "OK");
        }
    }

    private void OnDeleteAbilityClicked(object sender, EventArgs e)
    {
        var row = (sender as Button)?.CommandParameter as ClassAbilityRowVm
            ?? (sender as BindableObject)?.BindingContext as ClassAbilityRowVm;
        _vm.OwnerVm.RemoveAbilityFromLevel(_vm.LevelRow, row);
    }

    private async void OnDoneClicked(object sender, EventArgs e)
        => await Navigation.PopModalAsync();

    private async void OnCloseClicked(object sender, EventArgs e)
        => await Navigation.PopModalAsync();
}

public sealed class NonStandardAbilityLevelEditorVm
{
    public NonStandardAbilityLevelEditorVm(NonStandardClassCreateVm ownerVm, AbilityLevelVm levelRow)
    {
        OwnerVm = ownerVm;
        LevelRow = levelRow;
    }

    public NonStandardClassCreateVm OwnerVm { get; }
    public AbilityLevelVm LevelRow { get; }
    public string HeaderText => $"Abilities - Level {LevelRow.Level}";
}
