using System.Collections.ObjectModel;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardRaceAbilityRowEditorPage : ContentPage
{
    private readonly NonStandardRaceAbilityRowEditorVm _vm;

    public NonStandardRaceAbilityRowEditorPage(NonStandardCreateVm ownerVm, RaceAbilityRowVm row)
    {
        InitializeComponent();
        _vm = new NonStandardRaceAbilityRowEditorVm(ownerVm, row);
        BindingContext = _vm;
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        _vm.Apply();
        await Navigation.PopModalAsync();
    }

    private async void OnCloseClicked(object sender, EventArgs e)
        => await Navigation.PopModalAsync();
}

public sealed class NonStandardRaceAbilityRowEditorVm
{
    public NonStandardRaceAbilityRowEditorVm(NonStandardCreateVm ownerVm, RaceAbilityRowVm row)
    {
        OwnerVm = ownerVm;
        Row = row;
        SelectedLevel = row.Level;
        AbilityName = row.AbilityName;
        AbilityType = row.AbilityType;
        CountText = row.CountText;
    }

    public NonStandardCreateVm OwnerVm { get; }
    public RaceAbilityRowVm Row { get; }
    public ObservableCollection<int> LevelOptions { get; } = new(Enumerable.Range(1, 8));

    public int SelectedLevel { get; set; }
    public string AbilityName { get; set; } = string.Empty;
    public string AbilityType { get; set; } = string.Empty;
    public string CountText { get; set; } = string.Empty;

    public void Apply()
        => OwnerVm.ApplyRaceAbilityRowEdit(Row, SelectedLevel, AbilityName, AbilityType, CountText);
}
