using System.Threading.Tasks;

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
    }

    // Wait for result from parent
    public Task<RecipientInfo> GetRecipientAsync() => _tcs.Task;

    private async void OnSaveRecipient(object sender, EventArgs e)
    {
        var result = new RecipientInfo
        {
            PlayerName = RecipientPlayerNameEntry.Text?.Trim() ?? "",
            CharacterName = RecipientCharacterNameEntry.Text?.Trim() ?? "",
            CharacterClass = RecipientCharacterClassEntry.Text?.Trim() ?? ""
        };

        _tcs.TrySetResult(result);
        await Navigation.PopAsync();
    }
}

public class RecipientInfo
{
    public string PlayerName { get; set; } = "";
    public string CharacterName { get; set; } = "";
    public string CharacterClass { get; set; } = "";
}
