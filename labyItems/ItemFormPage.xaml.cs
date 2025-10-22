using labyItems.Models;
using labyItems.Services;
using labyItems.Pages;
namespace labyItems;

public partial class ItemFormPage : ContentPage
{
	private readonly Character _character;

	public ItemFormPage(Character character)
	{
   		InitializeComponent();
        _character = character;
        CharacterHeader.Text = $"Character: {_character.Name} ({_character.Class})";
        CreatedDatePicker.Date = DateTime.Now;
		ItemTypePicker.ItemsSource = Enum.GetValues(typeof(labyItems.Models.ItemTypeEnum)).Cast<labyItems.Models.ItemTypeEnum>().ToList();
        MakerPlayerNameEntry.Text = _character.PlayerName;
        MakerCharacterNameEntry.Text = _character.Name;
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

private async void OnCalculateIsp(object sender, EventArgs e)
{
    // Open calculator, await result (total + summary text)
    var page = new IspCalculator(ItemTypePicker.SelectedItem as ItemTypeEnum? ?? ItemTypeEnum.None);
    var result = await page.GetResultAsync(Navigation);
    if (result != null)
    {
        // Set ISP and append to description
        IspEntry.Text = result.TotalIsp.ToString();
        if (!string.IsNullOrWhiteSpace(result.Summary))
        {
            DescriptionEditor.Text = string.IsNullOrWhiteSpace(DescriptionEditor.Text)
                ? result.Summary
                : $"{DescriptionEditor.Text}\n{result.Summary}";
        }
    }
}

	private async void OnSubmitClicked(object sender, EventArgs e)
    {
        try
        {
            if (ItemTypePicker.SelectedItem is null)
            {
                await DisplayAlert("Error", "Please select an Item Type.", "OK");
                return;
            }

            var item = new Item
            {
                ItemType = (ItemTypeEnum)ItemTypePicker.SelectedItem,
                MakerPlayerName = MakerPlayerNameEntry.Text,
                MakerCharacterName = MakerCharacterNameEntry.Text,
                WitnessName = WitnessNameEntry.Text,
                RecipientPlayerName = RecipientPlayerNameEntry.Text,
                RecipientCharacterName = RecipientCharacterNameEntry.Text,
                Description = DescriptionEditor.Text,
                DoesNotBlowUpOnDeath = DnbuodSwitch.IsToggled,
                CreatedDate = CreatedDatePicker.Date,
                Isp = int.TryParse(IspEntry.Text, out var isp) ? isp : 0
            };

            await DisplayAlert("Item Created",
                $"\nType: {item.ItemType}" +
				$"\nMaker player name: {item.MakerPlayerName}" +
				$"\nMaker character name: {item.MakerCharacterName}" + 
				$"\nWitness name: {item.WitnessName}" +
				$"\nRecipient player name: {item.RecipientPlayerName}" +
				$"\nRecipient character name: {item.RecipientCharacterName}" +
				$"\nRecipient character class: {item.RecipientCharacterClass}" +
                $"\nISP: {item.Isp}\n" +
				$"\nDNBUOD: {item.DoesNotBlowUpOnDeath}\n" +
				$"\nCreated: {item.CreatedDate:d}", "OK");

			string subject = $"{item.MakerCharacterName} item for {item.RecipientCharacterName}";
    		string body = $"Type: {item.ItemType}\n" +
						$"Maker: {item.MakerPlayerName}\n" +
						$"Witness name: {item.WitnessName}\n" +
						$"Recipient player: {item.RecipientPlayerName}\n" +
						$"Recipient character: {item.RecipientCharacterName}\n" +
						$"Recipient character: {item.RecipientCharacterClass}\n" +
						$"Description: {item.Description}\n" +
						$"ISP: {item.Isp}\n" +
						$"Dnbuod: {item.DoesNotBlowUpOnDeath}\n" +
						$"Created: {item.CreatedDate:d}";

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

