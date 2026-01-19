namespace labyItems.Pages.Characters;

public partial class CharacterSpecialisation : ContentView
{
    public CharacterSpecialisation()
    {
        InitializeComponent();
    }

    public CharacterSpecialisation(CharacterBuilderVm builderVm) : this()
    {
        BindingContext = builderVm.SpecialisationVm;
    }
}
