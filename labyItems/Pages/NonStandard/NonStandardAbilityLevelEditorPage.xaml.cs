using labyItems.Models.Characters;
using labyItems.Pages.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;

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
            await Navigation.PushAsync(new AdvanceAbilitySearchPage(new NonStandardAbilitySearchHost(_vm.OwnerVm, _vm.LevelRow)));
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

internal sealed class NonStandardAbilitySearchHost : IAbilitySearchHost
{
    private readonly NonStandardClassCreateVm _ownerVm;
    private readonly AbilityLevelVm _levelRow;

    public NonStandardAbilitySearchHost(NonStandardClassCreateVm ownerVm, AbilityLevelVm levelRow)
    {
        _ownerVm = ownerVm;
        _levelRow = levelRow;
    }

    public CharacterDraft Draft { get; } = new();
    public bool AllowSpecialisationSelection => false;
    public bool DefaultAvailableOnly => false;
    public string TitleText => "Abilities";
    public string SubtitleText => "Search merged abilities and confirm the selections for this level";
    public string ConfirmButtonText => "Confirm";

    public void CommitSelection(IEnumerable<EvolutionService.AbilityResult>? selectedAbilities)
        => _ownerVm.ReplaceAbilitiesForLevel(_levelRow, selectedAbilities);
}
