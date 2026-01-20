using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public partial class CharacterInfoView : ContentView
{
    public CharacterInfoView(object bindingContext)
    {
        InitializeComponent();
        BindingContext = bindingContext;
    }
}
