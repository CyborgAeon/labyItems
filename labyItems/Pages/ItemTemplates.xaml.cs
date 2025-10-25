using labyItems.Models;
using labyItems.Services;
using LiteDB;

namespace labyItems.Pages;

public partial class ItemTemplates : ContentPage
{
    private readonly Action<Item> _onPicked;
    private readonly Character _character;
    public ItemTemplates(Action<Item> onPicked, Character character)
    {
        InitializeComponent();
        _onPicked = onPicked;
        _character = character;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadItems(_character.Id);
    }

    private void LoadItems(ObjectId characterId)
    {
        var items = LiteDbService.GetTemplatesByCharacterId(characterId).ToList();
        ItemsView.ItemsSource = items;
    }

    private async void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Item selected)
        {
            ((CollectionView)sender).SelectedItem = null;
            _onPicked?.Invoke(selected); // hand back to the form
            await Navigation.PopAsync(); // return to SubmitItemFormPage
        }
    }
}
