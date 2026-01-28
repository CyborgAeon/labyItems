using labyItems.Models;
using labyItems.Pages;
using labyItems.Services;

namespace labyItems.Pages.Characters;

public partial class CharacterBuilder : ContentView
{
    public CharacterBuilder()
    {
        InitializeComponent();
    }

    public CharacterBuilder(CharacterBuilderVm vm) : this()
    {
        BindingContext = vm;
    }
}