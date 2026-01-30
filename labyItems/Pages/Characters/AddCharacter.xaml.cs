using labyItems.Models;
using labyItems.Services;
using LiteDB;

namespace labyItems.Pages;

public partial class AddCharacter : ContentPage
{
    private ObjectId _charId;
    private readonly Character? _existingCharacter;
    public bool CanBeDeleted { get; private set; } = false;

    public AddCharacter(Character? character = null)
    {
        InitializeComponent();
        CanBeDeleted = character != null;
        _existingCharacter = character;
        if (character != null)
        {
            _charId = character.Id;
            Title = $"Edit {character.Name}";
            NameEntry.Text = character.Name;
            ClassEntry.Text = character.Class;
            PointsEntry.Text = character.Points.ToString();
            PlayerEntry.Text = character.PlayerName;
        }
        BindingContext = this;
    }

    private async void OnDelete(object sender, EventArgs e)
    {
        LiteDbService.DeleteChar(_charId);
         await Navigation.PopAsync();
    }

    private async void OnSave(object sender, EventArgs e)
    {
        if (
            string.IsNullOrWhiteSpace(NameEntry.Text)
            || string.IsNullOrWhiteSpace(ClassEntry.Text)
            || string.IsNullOrWhiteSpace(PointsEntry.Text)
            || string.IsNullOrWhiteSpace(PlayerEntry.Text)
        )
        {
            await DisplayAlert(
                "Missing info",
                "Character name, class & points are required.",
                "OK"
            );
            return;
        }

        var updated = _existingCharacter ?? new Character { Id = _charId };
        updated.Id = _charId;
        updated.Name = NameEntry.Text!.Trim();
        updated.Class = ClassEntry.Text!.Trim();
        updated.Points = long.Parse(PointsEntry.Text!.Trim());
        updated.PlayerName = PlayerEntry.Text?.Trim() ?? string.Empty;

        LiteDbService.UpsertCharacter(updated);

        await Navigation.PopAsync(); // back to list
    }
}
