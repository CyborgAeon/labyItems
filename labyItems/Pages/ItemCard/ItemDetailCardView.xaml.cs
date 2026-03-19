using System.Collections.ObjectModel;
using labyItems.Helpers;
using labyItems.Models;

namespace labyItems.Pages.ItemCard;

public partial class ItemDetailCardView : ContentView
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(
        nameof(Item),
        typeof(Item),
        typeof(ItemDetailCardView),
        default(Item),
        propertyChanged: OnItemChanged);

    public Item? Item
    {
        get => (Item?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public string DisplayName => ItemDisplayHelper.BuildDisplayName(Item);

    public string TypeText
    {
        get
        {
            var type = Item?.ItemType.ToString() ?? "None";
            return $"Type: {type}";
        }
    }

    public string SourceText
    {
        get
        {
            var source = (ItemDisplayHelper.TryGetPayload(Item)?.Item?.SourceFlow ?? string.Empty).Trim();
            if (source.Length == 0)
                source = "wallet";
            return $"Source: {source}";
        }
    }

    public string IspText => $"ISP: {Math.Max(0, Item?.Isp ?? 0)}";

    public string SummaryText
    {
        get
        {
            var summary = (Item?.Description ?? string.Empty).Trim();
            return summary.Length > 0 ? summary : "No summary available.";
        }
    }

    public ObservableCollection<ItemDetailPropertyRowVm> PropertyRows { get; } = new();
    public ObservableCollection<string> AbilityRows { get; } = new();
    public bool HasAbilityRows => AbilityRows.Count > 0;

    public ItemDetailCardView()
    {
        InitializeComponent();
        AbilityRows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAbilityRows));
    }

    private static void OnItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ItemDetailCardView view)
            return;

        view.RebuildRows();
    }

    private void RebuildRows()
    {
        PropertyRows.Clear();
        AbilityRows.Clear();

        var item = Item;
        if (item == null)
        {
            RaiseComputedProperties();
            return;
        }

        AddRow("Id", item.Id.ToString());
        AddRow("ItemType", item.ItemType.ToString());
        AddRow("Maker.Id", item.Maker?.Id.ToString());
        AddRow("Maker.Name", item.Maker?.Name);
        AddRow("Maker.PlayerName", item.Maker?.PlayerName);
        AddRow("Maker.Class", item.Maker?.Class);
        AddRow("Maker.Race", item.Maker?.Race);
        AddRow("WitnessName", item.WitnessName);
        AddRow("RecipientPlayerName", item.RecipientPlayerName);
        AddRow("RecipientCharacterName", item.RecipientCharacterName);
        AddRow("RecipientCharacterClass", item.RecipientCharacterClass);
        AddRow("Description", item.Description);
        AddRow("Isp", item.Isp.ToString());
        AddRow("CreatedDate", item.CreatedDate.ToString("dd MMM yyyy HH:mm"));
        AddRow("DoesNotBlowUpOnDeath", item.DoesNotBlowUpOnDeath.ToString());
        AddRow("AssignedCharacterId", item.AssignedCharacterId);
        AddRow("AssignedCharacterName", item.AssignedCharacterName);
        AddRow("AssignedCharacterPlayerName", item.AssignedCharacterPlayerName);

        var payload = ItemDisplayHelper.TryGetPayload(item);
        if (payload != null)
        {
            AddRow("Payload.WitnessName", payload.WitnessName);
            AddRow("Payload.Description", payload.Description);
            AddRow("Payload.Isp", payload.Isp.ToString());
            AddRow("Payload.CreatedDate", payload.CreatedDate.ToString("dd MMM yyyy HH:mm"));
            AddRow("Payload.DisplayName", payload.Item.DisplayName);
            AddRow("Payload.PhysicalRepresentation", payload.Item.PhysicalRepresentation);
            AddRow("Payload.SourceFlow", payload.Item.SourceFlow);
            AddRow("Payload.MonsterPointCost", payload.Item.MonsterPointCost.ToString());
            AddRow("Payload.Status", payload.Item.Status);
            if (payload.Item.Types.Count > 0)
                AddRow("Payload.Types", string.Join(", ", payload.Item.Types));

            foreach (var ability in payload.Item.Abilities)
            {
                var summary = (ability?.Summary ?? string.Empty).Trim();
                if (summary.Length > 0)
                    AbilityRows.Add(summary);
            }
        }

        var payloadJson = (item.PayloadJson ?? string.Empty).Trim();
        if (payloadJson.Length > 0)
            AddRow("PayloadJson", payloadJson);

        RaiseComputedProperties();
    }

    private void AddRow(string key, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return;

        PropertyRows.Add(new ItemDetailPropertyRowVm(key, text));
    }

    private void RaiseComputedProperties()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(IspText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasAbilityRows));
    }
}

public sealed record ItemDetailPropertyRowVm(string Key, string Value);
