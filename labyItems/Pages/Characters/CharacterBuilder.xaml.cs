using labyItems.Models;
using labyItems.Pages;
using labyItems.Services;

namespace labyItems.Pages.Characters;

public partial class CharacterBuilder : ContentView
{
    public CharacterBuilder()
    {
        InitializeComponent();
        // BindingContext should be set by the WizardVm when used in the wizard.
    }

    public CharacterBuilder(CharacterBuilderVm vm) : this()
    {
        BindingContext = vm;
    }
}