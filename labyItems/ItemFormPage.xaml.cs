using System.Collections.Generic;
using System.Linq;
using labyItems.Models;
using labyItems.Services;
using labyItems.Pages;
using labyItems.Pages.Calculator;
using labyItemsq.Helpers;

namespace labyItems;
public partial class ItemFormPage : ContentPage
{
    public int IspTotal {get;set;} = 0;
    public bool ShowPlayerNameField { get; set; } = false;
    public bool ShowPlayerCharNameField { get; set; } = false;
    private readonly Character _character;
    private string _recipientName;
    private string _recipientClass;
    private string _recipientPlayerName;
    private List<CalcResult> _abilities = new();
    private bool _userSetBase;
    private int _manualBaseIsp;
    public ItemFormPage(Character character)
    {
        InitializeComponent();
        _character = character;
        ShowPlayerNameField = string.IsNullOrWhiteSpace(_character.PlayerName);
        ShowPlayerCharNameField = string.IsNullOrWhiteSpace(_character.Name);
        CharacterHeader.Text = $"{_character.Name} ({_character.Class}): {_character.Points.ToKNotation()}";
        CreatedDatePicker.Date = DateTime.Now;
        MakerPlayerNameEntry.Text = _character.PlayerName;
        MakerCharacterNameEntry.Text = _character.Name;
        BindingContext = this;
    }

    private async void OnViewSavedClicked(object sender, EventArgs e)
    {
        // Push list page; when an item is picked, we’ll get it here
        await Navigation.PushAsync(new ItemTemplates(OnItemPicked, _character));
    }

    // This gets called by ItemsListPage when user taps an item
    private void OnItemPicked(Item picked)
    {
        LoadFromItem(picked);
    }

    private void LoadFromItem(Item item)
    {
        MakerPlayerNameEntry.Text = item.Maker.PlayerName;
        MakerCharacterNameEntry.Text = item.Maker.Name;
        DescriptionEditor.Text = item.Description;
        IspTotal = item.Isp;
        CreatedDatePicker.Date = item.CreatedDate;
        DnbuodSwitch.IsToggled = item.DoesNotBlowUpOnDeath;
        _abilities = new();
        _userSetBase = true;
        _manualBaseIsp = item.Isp;
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
        var baseForCalc = _userSetBase ? _manualBaseIsp : ExtractBaseFromAbilities();
        var page = new IspCalculator(baseForCalc, _abilities);
        var result = await page.GetResultAsync(Navigation);
        if (result == null) return;
        _abilities = result.Abilities ?? new();
        var baseAbility = _abilities.FirstOrDefault(a => string.Equals(a.AbilityType, "Base", StringComparison.OrdinalIgnoreCase));
        _manualBaseIsp = baseAbility?.TotalIsp ?? 0;
        _userSetBase = baseAbility != null;
        IspTotal = result.TotalIsp;
        IspEntry.Text = result.TotalIsp.ToString();

        if (!string.IsNullOrWhiteSpace(result.SummaryText))
        {
            DescriptionEditor.Text = string.IsNullOrWhiteSpace(DescriptionEditor.Text)
                ? result.SummaryText
                : $"{DescriptionEditor.Text}\n{result.SummaryText}";
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        try
        {
        var item = new Item
        {
            Maker = new Character
            {
                Id = _character.Id,
                    PlayerName = _character.PlayerName ?? MakerPlayerNameEntry.Text,
                    Name = _character.Name ?? MakerCharacterNameEntry.Text,
                    Points = _character.Points,
                },
                WitnessName = WitnessNameEntry.Text,
                RecipientPlayerName = _recipientPlayerName,
                RecipientCharacterName = _recipientName,
                RecipientCharacterClass = _recipientClass,
                Description = DescriptionEditor.Text,
                DoesNotBlowUpOnDeath = DnbuodSwitch.IsToggled,
                CreatedDate = CreatedDatePicker.Date,
                Isp = IspTotal,
            };

            var recipientText = _recipientPlayerName == string.Empty ?
                            "\nno recipient" : $"\nRecipient player name: {item.RecipientPlayerName}" +
                            $"\nRecipient character name: {item.RecipientCharacterName}" +
                            $"\nRecipient character class: {item.RecipientCharacterClass}";

            await DisplayAlert("Item Created",
                $"\nMaker player name: {item.Maker.PlayerName}" +
                $"\nMaker character name: {item.Maker.Name}" +
                $"\nMaker character points: {item.Maker.Points}" +
                $"\nWitness name: {item.WitnessName}" +
                recipientText +
                $"\nType: {item.ItemType}" +
                $"\nISP: {item.Isp}\n" +
                $"\nDNBUOD: {item.DoesNotBlowUpOnDeath}\n" +
                $"\nCreated: {item.CreatedDate:d}", "Continue");

            string subject = $"{item.Maker.Name} item for {item.RecipientCharacterName}";
            string body = $"\nType: {item.ItemType}" +
                        $"\nMaker: {item.Maker.PlayerName}" +
                        $"\nWitness name: {item.WitnessName}" +
                        recipientText +
                        $"\nDescription: {item.Description}" +
                        $"\nISP: {item.Isp}" +
                        $"\nDnbuod: {item.DoesNotBlowUpOnDeath}" +
                        $"\nCreated: {item.CreatedDate:d}";

            string mailto = $"mailto:items@labyrinthe.com" +
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

    private int ExtractBaseFromAbilities()
    {
        var baseAbility = _abilities.FirstOrDefault(a => string.Equals(a.AbilityType, "Base", StringComparison.OrdinalIgnoreCase));
        return baseAbility?.TotalIsp ?? 0;
    }
}
