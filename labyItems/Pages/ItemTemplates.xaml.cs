using labyItems.Models;
using labyItems.Services;

namespace labyItems.Pages;

public partial class ItemTemplates : ContentPage
{
    private readonly Action<Item> _onPicked;

    public ItemTemplates(Action<Item> onPicked)
    {
        InitializeComponent();
        _onPicked = onPicked;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadItems();
    }

    private void LoadItems()
    {
        var items = LiteDbService.GetLatestTemplates().ToList();
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
