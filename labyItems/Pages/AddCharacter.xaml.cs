using labyItems.Models;
using labyItems.Services;
using LiteDB;

namespace labyItems.Pages;

public partial class AddCharacter : ContentPage
{
    private ObjectId _charId;
    public AddCharacter(Character? character = null)
    {
        InitializeComponent();
        if (character != null)
        {
            Title = $"Edit {character.Name}";
            NameEntry.Text = character.Name;
            ClassEntry.Text = character.Class;
            PointsEntry.Text = character.Points.ToString();
            PlayerEntry.Text = character.PlayerName;
            _charId = character.Id;
        }
        BindingContext = this;
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
            Id = _charId,
            Name = NameEntry.Text!.Trim(),
            Class = ClassEntry.Text!.Trim(),
            Points = long.Parse(PointsEntry.Text!.Trim()),
            PlayerName = PlayerEntry.Text?.Trim() ?? string.Empty
        });

        await Navigation.PopAsync(); // back to list
    }
}
