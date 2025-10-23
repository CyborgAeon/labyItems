using System.Threading.Tasks;
using labyItems.Models;

namespace labyItems.Pages;

public partial class RecipientPage : ContentPage
{
    private readonly TaskCompletionSource<RecipientInfo> _tcs = new();

    public RecipientPage(RecipientInfo? existing = null)
    {
        InitializeComponent();
        if (existing != null)
        {
            RecipientPlayerNameEntry.Text = existing.PlayerName;
            RecipientCharacterNameEntry.Text = existing.CharacterName;
            RecipientCharacterClassEntry.Text = existing.CharacterClass;
        }
        BindingContext = this;
    }

    // Wait for result from parent
    public Task<RecipientInfo> GetRecipientAsync() => _tcs.Task;

    private async void OnSaveRecipient(object sender, EventArgs e)
    {
        var result = new RecipientInfo
        {
            PlayerName = RecipientPlayerNameEntry.Text?.Trim() ?? string.Empty,
            CharacterName = RecipientCharacterNameEntry.Text?.Trim() ?? string.Empty,
            CharacterClass = RecipientCharacterClassEntry.Text?.Trim() ?? string.Empty
        };

        _tcs.TrySetResult(result);
        await Navigation.PopAsync();
    }
}
