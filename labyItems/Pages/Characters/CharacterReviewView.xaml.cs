using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewView : ContentView
{
    public CharacterReviewView(object bindingContext)
    {
        InitializeComponent();
        BindingContext = bindingContext;
    }
}
