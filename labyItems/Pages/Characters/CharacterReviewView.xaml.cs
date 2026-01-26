using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewView : ContentView
{
    public static readonly BindableProperty ShowSaveButtonProperty = BindableProperty.Create(
        nameof(ShowSaveButton),
        typeof(bool),
        typeof(CharacterReviewView),
        true);

    public bool ShowSaveButton
    {
        get => (bool)GetValue(ShowSaveButtonProperty);
        set => SetValue(ShowSaveButtonProperty, value);
    }

    public CharacterReviewView(object bindingContext)
    {
        InitializeComponent();
        BindingContext = bindingContext;
    }

    public CharacterReviewView()
    {
        InitializeComponent();
    }
}
