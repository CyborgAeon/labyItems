using labyItems.Models;
using labyItems.Services;

namespace labyItems.Pages;

public partial class CharactersPage : ContentPage
{
    public CharactersPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadCharacters();
    }

    void LoadCharacters()
    {
        var list = LiteDbService.GetCharacters().ToList();
        CharsView.ItemsSource = list;
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AddCharacter());
    }

    private async void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Character c)
        {
            ((CollectionView)sender).SelectedItem = null; // clear selection
            await Navigation.PushAsync(new ItemFormPage(c)); // go to form
        }
    }
}
