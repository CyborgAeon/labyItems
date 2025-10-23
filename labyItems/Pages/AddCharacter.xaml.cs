using labyItems.Models;
using labyItems.Services;

namespace labyItems.Pages;

public partial class AddCharacter : ContentPage
{
    public AddCharacter()
    {
        InitializeComponent();
    }

    private async void OnSave(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEntry.Text) ||
        string.IsNullOrWhiteSpace(ClassEntry.Text) ||
        string.IsNullOrWhiteSpace(PointsEntry.Text))
        {
            await DisplayAlert("Missing info", "Character name, class & points are required.", "OK");
            return;
        }

        LiteDbService.UpsertCharacter(new Character
        {
            Name = NameEntry.Text!.Trim(),
            Class = ClassEntry.Text!.Trim(),
            Points = long.Parse(PointsEntry.Text!.Trim()),
            PlayerName = PlayerEntry.Text?.Trim() ?? string.Empty
        });

        await Navigation.PopAsync(); // back to list
    }
}
