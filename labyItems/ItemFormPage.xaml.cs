using labyItems.Models;
using labyItems.Services;
using labyItems.Pages;
using labyItemsq.Helpers;
namespace labyItems;

public partial class ItemFormPage : ContentPage
{
    public bool ShowPlayerNameField { get; set; } = false;
    public bool ShowPlayerCharNameField { get; set; } = false;
    private readonly Character _character;
    private string _recipientName;
    private string _recipientClass;
    private string _recipientPlayerName;
    public ItemFormPage(Character character)
    {
        InitializeComponent();
        _character = character;
        ShowPlayerNameField = string.IsNullOrWhiteSpace(_character.PlayerName);
        ShowPlayerCharNameField = string.IsNullOrWhiteSpace(_character.Name);
        CharacterHeader.Text = $"Character: {_character.Name} ({_character.Class}): {_character.Points.ToKNotation()}";
        CreatedDatePicker.Date = DateTime.Now;
        ItemTypePicker.ItemsSource = Enum.GetValues(typeof(ItemTypeEnum)).Cast<ItemTypeEnum>().ToList();
        ItemTypePicker.SelectedItem = ItemTypeEnum.None;
        MakerPlayerNameEntry.Text = _character.PlayerName;
        MakerCharacterNameEntry.Text = _character.Name;
        BindingContext = this;
    }

    private async void OnViewSavedClicked(object sender, EventArgs e)
    {
        // Push list page; when an item is picked, we’ll get it here
        await Navigation.PushAsync(new ItemTemplates(OnItemPicked));
    }

    // This gets called by ItemsListPage when user taps an item
    private void OnItemPicked(Item picked)
    {
        LoadFromItem(picked);
    }

    private void LoadFromItem(Item item)
    {
        ItemTypePicker.SelectedItem = item.ItemType;
        MakerPlayerNameEntry.Text = item.MakerPlayerName;
        MakerCharacterNameEntry.Text = item.MakerCharacterName;
        DescriptionEditor.Text = item.Description;
        IspEntry.Text = item.Isp.ToString();
        CreatedDatePicker.Date = item.CreatedDate;
        DnbuodSwitch.IsToggled = item.DoesNotBlowUpOnDeath;
    }

    private async void OnAddRecipientClicked(object sender, EventArgs e)
    {
        var recipient = new RecipientInfo();
        if (!string.IsNullOrWhiteSpace(_recipientName) &&
            !string.IsNullOrWhiteSpace(_recipientClass) &&
            !string.IsNullOrWhiteSpace(_recipientPlayerName))
        {
            recipient = new RecipientInfo
            {
                CharacterName = _recipientName,
                CharacterClass = _recipientClass,
                PlayerName = _recipientPlayerName,
            };
        }

        var page = new RecipientPage(recipient);
        await Navigation.PushAsync(page);
        var response = await page.GetRecipientAsync();
        if (response != null)
        {
            _recipientName = response.CharacterName;
            _recipientClass = response.CharacterClass;
            _recipientPlayerName = response.PlayerName;

            RecipientButton.Text = "Adjust Recipient";
        }
        else
        {
            _recipientName = string.Empty;
            _recipientClass = string.Empty;
            _recipientPlayerName = string.Empty;

            RecipientButton.Text = "Add Recipient";
        }
    }

    private async void OnCalculateIsp(object sender, EventArgs e)
    {
        var page = new IspCalculator(ItemTypePicker.SelectedItem as ItemTypeEnum? ?? ItemTypeEnum.None);
        var result = await page.GetResultAsync(Navigation);
        if (result == null) return;
        var isp = int.Parse(IspEntry.Text ?? "0") + result.TotalIsp;
        IspEntry.Text = isp.ToString();
        if (!string.IsNullOrWhiteSpace(result.Summary))
        {
            DescriptionEditor.Text = string.IsNullOrWhiteSpace(DescriptionEditor.Text)
                ? result.Summary
                : $"{DescriptionEditor.Text}\n{result.Summary}";
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        try
        {
            var item = new Item
            {
                ItemType = (ItemTypeEnum)ItemTypePicker.SelectedItem,
                MakerPlayerName = _character.PlayerName ?? MakerPlayerNameEntry.Text,
                MakerCharacterName = _character.Name ?? MakerCharacterNameEntry.Text,
                MakerCharacterPoints = _character.Points,
                WitnessName = WitnessNameEntry.Text,
                RecipientPlayerName = _recipientPlayerName,
                RecipientCharacterName = _recipientName,
                RecipientCharacterClass = _recipientClass,
                Description = DescriptionEditor.Text,
                DoesNotBlowUpOnDeath = DnbuodSwitch.IsToggled,
                CreatedDate = CreatedDatePicker.Date,
                Isp = int.TryParse(IspEntry.Text, out var isp) ? isp : 0
            };

            var recipientText = _recipientPlayerName == string.Empty ?
                            "\nno recipient" : $"\nRecipient player name: {item.RecipientPlayerName}" +
                            $"\nRecipient character name: {item.RecipientCharacterName}" +
                            $"\nRecipient character class: {item.RecipientCharacterClass}";

            await DisplayAlert("Item Created",
                $"\nMaker player name: {item.MakerPlayerName}" +
                $"\nMaker character name: {item.MakerCharacterName}" +
                $"\nMaker character points: {item.MakerCharacterPoints}" +
                $"\nWitness name: {item.WitnessName}" +
                recipientText +
                $"\nType: {item.ItemType}" +
                $"\nISP: {item.Isp}\n" +
                $"\nDNBUOD: {item.DoesNotBlowUpOnDeath}\n" +
                $"\nCreated: {item.CreatedDate:d}", "OK");

            string subject = $"{item.MakerCharacterName} item for {item.RecipientCharacterName}";
            string body = $"\nType: {item.ItemType}" +
                        $"\nMaker: {item.MakerPlayerName}" +
                        $"\nWitness name: {item.WitnessName}" +
                        recipientText +
                        $"\nDescription: {item.Description}" +
                        $"\nISP: {item.Isp}" +
                        $"\nDnbuod: {item.DoesNotBlowUpOnDeath}" +
                        $"\nCreated: {item.CreatedDate:d}";

            string mailto = $"mailto:bradleyjamesbarfoot@gmail.com" +
                            $"?subject={Uri.EscapeDataString(subject)}" +
                            $"&body={Uri.EscapeDataString(body)}";

            try
            {
                await Launcher.OpenAsync(mailto);
                LiteDbService.InsertItem(item);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not open mail client: {ex.Message}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}

