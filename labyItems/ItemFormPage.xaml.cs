using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
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
            var payload = BuildJsonPayload(item);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            jsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            var json = JsonSerializer.Serialize(payload, jsonOptions);
            var compressedJson = JsonTokenCompressor.CompressToBase64(json);

            string body =
                $"\nType: {item.ItemType}"
                + $"\nMaker: {item.Maker.PlayerName}"
                + $"\nWitness name: {item.WitnessName}"
                + recipientText
                + $"\nDescription: {item.Description}"
                + $"\nISP: {item.Isp}"
                + $"\nDnbuod: {item.DoesNotBlowUpOnDeath}"
                + $"\nCreated: {item.CreatedDate:d}"
                + "\n\n Token:\n"
                + compressedJson;

            string mailto =
                $"mailto:items@labyrinthe.com"
                + $"?subject={Uri.EscapeDataString(subject)}"
                + $"&body={Uri.EscapeDataString(body)}";

            try
            {
                await Launcher.OpenAsync(mailto);
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

    private ItemJsonPayload BuildJsonPayload(Item item)
    {
        var derivedTypes = DeriveItemTypes();
        var abilities = _abilities?.ToList() ?? new List<CalcResult>();
        if (abilities.Count == 0 && item.Isp > 0)
        {
            abilities.Add(new CalcResult
            {
                AbilityType = "Base",
                AbilityName = "Manual ISP entry",
                TotalIsp = item.Isp,
                Details = new() { ["source"] = "ItemForm" }
            });
        }

        var payload = new ItemJsonPayload
        {
            WitnessName = item.WitnessName,
            Recipient = string.IsNullOrWhiteSpace(_recipientPlayerName)
                        && string.IsNullOrWhiteSpace(_recipientName)
                        && string.IsNullOrWhiteSpace(_recipientClass)
                        ? null
                        : new RecipientPayload
                        {
                            PlayerName = _recipientPlayerName,
                            CharacterName = _recipientName,
                            CharacterClass = _recipientClass
                        },
            Description = item.Description,
            Isp = item.Isp,
            CreatedDate = item.CreatedDate,
            Item = new ItemJsonDetail
            {
                Types = derivedTypes.Where(t => t != ItemTypeEnum.None).DefaultIfEmpty(ItemTypeEnum.None).ToList(),
                Abilities = abilities,
                Status = "TODO",
                Modifiers = new List<object>() // TODO: flesh out modifiers model when available.
            }
        };

        return payload;
    }

    private List<ItemTypeEnum> DeriveItemTypes()
    {
        bool isSpiritual = _abilities.Any(IsSpiritualAbility);
        bool isEarthpower = _abilities.Any(IsEarthpowerAbility);

        var types = new List<ItemTypeEnum>();
        if (isEarthpower) types.Add(ItemTypeEnum.EarthPower);
        if (isSpiritual) types.Add(ItemTypeEnum.Spirit);
        if (types.Count == 0) types.Add(ItemTypeEnum.None);
        return types;
    }

    private static bool IsSpiritualAbility(CalcResult result)
    {
        if (result.AbilityType.Equals("Miracle", StringComparison.OrdinalIgnoreCase))
        {
            return HasPositive(result, "basicPerDay", "advancedPerDay", "generalSpiritStore", "sphereSpiritStore",
                               "turnBasicUpTo5thMantic", "turnBasicMantic", "turnAdvancedUpTo6thMantic", "turnAdvancedAbove6thMantic", "trueBeliever")
                   || HasTrue(result, "addBasicToList", "addAdvancedToList", "addWithPrep30", "isTeachingScroll");
        }

        if (result.AbilityType.Equals("General", StringComparison.OrdinalIgnoreCase))
        {
            var spiritPrayer = GetString(result, "prayerPowerbase")?.Equals("Spirit", StringComparison.OrdinalIgnoreCase) == true
                               && GetInt(result, "prayerTimesPerDay") > 0;

            return HasTrue(result, "empowerWeaponSpirit", "empowerWeaponMantic", "undeadTouchEffect", "gaseousForm", "walkThroughWalls", "planeShift")
                   || spiritPrayer;
        }

        return false;
    }

    private static bool IsEarthpowerAbility(CalcResult result)
    {
        if (result.AbilityType.Equals("Evocation", StringComparison.OrdinalIgnoreCase))
        {
            return HasPositive(result, "basicPerDay", "advancedPerDay", "drawOnEpPerDay")
                   || HasTrue(result, "addBasicToList", "addAdvancedToList", "addWithPrep30");
        }

        return false;
    }

    private static bool HasPositive(CalcResult res, params string[] keys) =>
        keys.Any(k => GetInt(res, k) > 0);

    private static bool HasTrue(CalcResult res, params string[] keys) =>
        keys.Any(k => GetBool(res, k));

    private static int GetInt(CalcResult res, string key)
    {
        if (res.Details.TryGetValue(key, out var v))
        {
            if (v is int i) return i;
            if (v is long l) return (int)l;
        }
        return 0;
    }

    private static bool GetBool(CalcResult res, string key)
    {
        if (res.Details.TryGetValue(key, out var v))
        {
            if (v is bool b) return b;
            if (v is int i) return i > 0;
            if (v is long l) return l > 0;
            if (v is string s && bool.TryParse(s, out var parsed)) return parsed;
        }
        return false;
    }

    private static string? GetString(CalcResult res, string key)
    {
        if (res.Details.TryGetValue(key, out var v))
            return v?.ToString();
        return null;
    }
}
