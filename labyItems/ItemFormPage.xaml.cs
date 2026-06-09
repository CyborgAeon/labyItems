using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Pages;
using labyItems.Pages.Calculator;
using labyItems.Services;

namespace labyItems;

public partial class ItemFormPage : ContentPage
{
    public int IspTotal { get; set; } = 0;
    public bool ShowPlayerNameField { get; set; } = false;
    public bool ShowPlayerCharNameField { get; set; } = false;
    public ObservableCollection<string> DescriptionLines { get; } = new();
    private readonly Character _character;
    private string _recipientName;
    private string _recipientClass;
    private string _recipientPlayerName;
    private List<CalcResult> _abilities = new();
    private bool _userSetBase;

    public ItemFormPage(Character character)
    {
        InitializeComponent();
        _character = character;
        ShowPlayerNameField = string.IsNullOrWhiteSpace(_character.PlayerName);
        ShowPlayerCharNameField = string.IsNullOrWhiteSpace(_character.Name);
        CharacterHeader.Text =
            $"{_character.Name} ({_character.Class}): {_character.Points.ToKNotation()}";
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
        SetDescriptionFromText(item.Description);
        IspTotal = item.Isp;
        CreatedDatePicker.Date = item.CreatedDate;
        DnbuodSwitch.IsToggled = item.DoesNotBlowUpOnDeath;
        _abilities = new();
        _userSetBase = true;
    }

    private async void OnAddRecipientClicked(object sender, EventArgs e)
    {
        var recipient = new RecipientInfo();
        if (
            !string.IsNullOrWhiteSpace(_recipientName)
            && !string.IsNullOrWhiteSpace(_recipientClass)
            && !string.IsNullOrWhiteSpace(_recipientPlayerName)
        )
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
        var page = new IspCalculator(0, _abilities);
        var result = await page.GetResultAsync(Navigation);
        if (result == null)
            return;
        _abilities = result.Abilities ?? new();
        var baseAbility = _abilities.FirstOrDefault(a =>
            string.Equals(a.AbilityType, "Base", StringComparison.OrdinalIgnoreCase)
        );
        _userSetBase = baseAbility != null;
        IspTotal = result.TotalIsp;
        IspEntry.Text = result.TotalIsp.ToString();

        AppendDescriptionLine(result.SummaryText);
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
                Description = GetDescriptionText(),
                DoesNotBlowUpOnDeath = DnbuodSwitch.IsToggled,
                CreatedDate = CreatedDatePicker.Date ?? DateTime.Now,
                Isp = IspTotal,
            };

            var recipientText =
                _recipientPlayerName == string.Empty
                    ? "\nno recipient"
                    : $"\nRecipient player name: {item.RecipientPlayerName}"
                        + $"\nRecipient character name: {item.RecipientCharacterName}"
                        + $"\nRecipient character class: {item.RecipientCharacterClass}";

            await DisplayAlert(
                "Item Created",
                $"\nMaker player name: {item.Maker.PlayerName}"
                    + $"\nMaker character name: {item.Maker.Name}"
                    + $"\nMaker character points: {item.Maker.Points}"
                    + $"\nWitness name: {item.WitnessName}"
                    + recipientText
                    + $"\nType: {item.ItemType}"
                    + $"\nISP: {item.Isp}\n"
                    + $"\nDNBUOD: {item.DoesNotBlowUpOnDeath}\n"
                    + $"\nCreated: {item.CreatedDate:d}",
                "Continue"
            );

            string subject = $"{item.Maker.Name} item for {item.RecipientCharacterName}";
            var payload = ItemEmailService.BuildItemPayload(item, _abilities);
            item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);
            var emailDraft = ItemEmailService.BuildItemEmailDraft(
                item,
                payload,
                to: "brbar@netcompany.com",
                subject: subject);

            try
            {
                await Launcher.OpenAsync(emailDraft.MailtoUri);
                LiteDbService.InsertItem(item);
                RemoveItemFromPage();
                await Navigation.PopToRootAsync();
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

    private void RemoveItemFromPage()
    {
        IspTotal = 0;
        _abilities = new();
        _recipientName = string.Empty;
        _recipientClass = string.Empty;
        DnbuodSwitch.IsToggled = false;
        _recipientPlayerName = string.Empty;
        WitnessNameEntry.Text = string.Empty;
        CreatedDatePicker.Date = DateTime.Now;
    }

    private void SetDescriptionFromText(string? text)
    {
        DescriptionLines.Clear();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var lines = text
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l));

        foreach (var line in lines)
            DescriptionLines.Add(line);
    }

    private void AppendDescriptionLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        DescriptionLines.Add(line.Trim());
    }

    private string GetDescriptionText() => string.Join("\n", DescriptionLines);
}
