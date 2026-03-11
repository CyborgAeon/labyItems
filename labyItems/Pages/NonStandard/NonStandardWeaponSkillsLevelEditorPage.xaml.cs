namespace labyItems.Pages.NonStandard;

public partial class NonStandardWeaponSkillsLevelEditorPage : ContentPage
{
    public NonStandardWeaponSkillsLevelEditorPage(NonStandardClassCreateVm ownerVm, WeaponSkillLevelVm levelRow)
    {
        InitializeComponent();
        BindingContext = new NonStandardWeaponSkillsLevelEditorVm(ownerVm, levelRow);
    }

    private async void OnDoneClicked(object sender, EventArgs e)
        => await Navigation.PopModalAsync();

    private async void OnCloseClicked(object sender, EventArgs e)
        => await Navigation.PopModalAsync();
}

public sealed class NonStandardWeaponSkillsLevelEditorVm
{
    public NonStandardWeaponSkillsLevelEditorVm(NonStandardClassCreateVm ownerVm, WeaponSkillLevelVm levelRow)
    {
        OwnerVm = ownerVm;
        LevelRow = levelRow;
    }

    public NonStandardClassCreateVm OwnerVm { get; }
    public WeaponSkillLevelVm LevelRow { get; }
    public IReadOnlyList<string> WeaponSkillOptions => OwnerVm.WeaponSkillOptions;
    public string HeaderText => $"Weapon Skills - Level {LevelRow.Level}";
}
