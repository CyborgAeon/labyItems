using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages;

public partial class ItemWalletPage : ContentPage
{
    public ObservableCollection<ItemWalletCardVm> ItemWalletRows { get; } = new();

    public ItemWalletPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshItemWallet();
    }

    private void OnItemWalletCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not ItemWalletCardVm tapped)
            return;

        var shouldExpand = !tapped.IsExpanded;
        foreach (var row in ItemWalletRows)
            row.IsExpanded = ReferenceEquals(row, tapped) && shouldExpand;
    }

    private async void OnDeleteItemClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not ItemWalletCardVm card)
            return;

        var confirmed = await DisplayAlert("Delete item", "Delete this item from wallet?", "Delete", "Cancel");
        if (!confirmed)
            return;

        LiteDbService.DeleteItem(card.Item.Id);
        RefreshItemWallet();
    }

    private async void OnAssignItemClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not ItemWalletCardVm card)
            return;

        await AssignItemToCharacterAsync(card, allowUnassign: false);
    }

    private async void OnChangeAssignmentClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not ItemWalletCardVm card)
            return;

        await AssignItemToCharacterAsync(card, allowUnassign: true);
    }

    private async void OnEmailItemClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not ItemWalletCardVm card)
            return;

        try
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(card.Item.PayloadJson)
                         ?? ItemEmailService.BuildItemPayload(card.Item);
            var draft = ItemEmailService.BuildItemEmailDraft(card.Item, payload);
            await Launcher.OpenAsync(draft.MailtoUri);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Email failed", ex.Message, "OK");
        }
    }

    private async void OnDetailRowInfoClicked(object sender, EventArgs e)
    {
        ItemWalletDetailRowVm? row = null;
        if (sender is BindableObject bindable && bindable.BindingContext is ItemWalletDetailRowVm bound)
            row = bound;
        else if (sender is Button button && button.CommandParameter is ItemWalletDetailRowVm parameterRow)
            row = parameterRow;

        if (row == null || !row.HasInfoButton)
            return;

        try
        {
            var ability = await row.ResolveAbilityAsync();
            if (ability == null)
            {
                await DisplayAlert("No details", "No linked ability details were found for this row yet.", "OK");
                return;
            }

            var nav = Navigation ?? Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
            if (nav == null)
                return;

            await nav.PushAsync(new AbilityCardPage(ability));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Unable to open details", ex.Message, "OK");
        }
    }

    private async Task AssignItemToCharacterAsync(ItemWalletCardVm card, bool allowUnassign)
    {
        var characters = LiteDbService.GetCharacters().ToList();
        if (characters.Count == 0)
        {
            await DisplayAlert("No characters", "Create a character first, then assign this item.", "OK");
            return;
        }

        var options = characters
            .Select(c => string.IsNullOrWhiteSpace(c.PlayerName) ? c.Name : $"{c.Name} ({c.PlayerName})")
            .ToList();

        var selected = await DisplayActionSheet(
            "Assign item to character",
            "Cancel",
            allowUnassign ? "Unassign" : null,
            options.ToArray());

        if (string.Equals(selected, "Cancel", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(selected))
            return;

        if (allowUnassign && string.Equals(selected, "Unassign", StringComparison.OrdinalIgnoreCase))
        {
            card.Item.AssignedCharacterId = string.Empty;
            card.Item.AssignedCharacterName = string.Empty;
            card.Item.AssignedCharacterPlayerName = string.Empty;
            LiteDbService.UpdateItem(card.Item);
            card.Refresh();
            return;
        }

        var selectedIndex = options.FindIndex(o => string.Equals(o, selected, StringComparison.Ordinal));
        if (selectedIndex < 0 || selectedIndex >= characters.Count)
            return;

        var chosen = characters[selectedIndex];
        card.Item.AssignedCharacterId = chosen.Id;
        card.Item.AssignedCharacterName = chosen.Name ?? string.Empty;
        card.Item.AssignedCharacterPlayerName = chosen.PlayerName ?? string.Empty;

        if (!LiteDbService.UpdateItem(card.Item))
            LiteDbService.InsertItem(card.Item);

        card.Refresh();
    }

    private void RefreshItemWallet()
    {
        ItemWalletRows.Clear();
        var rows = LiteDbService.GetItems()
            .Take(200)
            .Select(item => new ItemWalletCardVm(item))
            .ToList();

        foreach (var row in rows)
            ItemWalletRows.Add(row);

        if (EmptyItemWalletLabel != null)
            EmptyItemWalletLabel.IsVisible = ItemWalletRows.Count == 0;
    }

    public sealed class ItemWalletCardVm : INotifyPropertyChanged
    {
        private static readonly Regex EquationRegex = new(
            @"^(?<label>.+?)\s*=\s*(?<cost>-?\d+)(?:\s*\(\s*-?\d+\s*\))?$",
            RegexOptionsCompat.ForRuntime(RegexOptions.Compiled | RegexOptions.CultureInvariant));

        private static readonly Color RowEvenColor = Colors.White;
        private static readonly Color RowOddColor = Color.FromArgb("#F6F6F6");
        private bool _isExpanded;

        public ItemWalletCardVm(Item item)
        {
            Item = item;
            DetailRows = BuildDetailRows(item);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Item Item { get; }

        public string HeaderText => BuildHeader(Item);

        public ObservableCollection<ItemWalletDetailRowVm> DetailRows { get; private set; }

        public bool ShowAssignButton => string.IsNullOrWhiteSpace(Item.AssignedCharacterName);
        public bool ShowChangeButton => !ShowAssignButton;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandGlyph));
            }
        }

        public string ExpandGlyph => IsExpanded ? "⌄" : "›";

        public void Refresh()
        {
            DetailRows = BuildDetailRows(Item);
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(DetailRows));
            OnPropertyChanged(nameof(ShowAssignButton));
            OnPropertyChanged(nameof(ShowChangeButton));
        }

        private static ObservableCollection<ItemWalletDetailRowVm> BuildDetailRows(Item item)
        {
            var rows = BuildRows(item);
            var result = new ObservableCollection<ItemWalletDetailRowVm>();
            for (int i = 0; i < rows.Count; i++)
                result.Add(new ItemWalletDetailRowVm(
                    rows[i].Text,
                    i % 2 == 0 ? RowEvenColor : RowOddColor,
                    rows[i].AbilityLinkHint));
            return result;
        }

        private static List<ItemWalletRowData> BuildRows(Item item)
        {
            var sourceLines = (item.Description ?? string.Empty)
                .Split('\n')
                .Select(line => NormalizeLine(line))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var visibleLines = sourceLines
                .Where(line => !IsHiddenLine(line))
                .ToList();

            var infoRows = new List<string>();
            var seenInfo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var equationOrder = new List<string>();
            var equationCosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in visibleLines)
            {
                if (TryParseEquation(line, out var label, out var cost))
                {
                    if (!equationCosts.ContainsKey(label))
                        equationOrder.Add(label);
                    equationCosts[label] = equationCosts.TryGetValue(label, out var existing) ? existing + cost : cost;
                    continue;
                }

                if (seenInfo.Add(line))
                    infoRows.Add(line);
            }

            var rows = new List<ItemWalletRowData>();
            rows.AddRange(infoRows.Select(text => new ItemWalletRowData(
                text,
                ItemAbilityLinkService.TryCreateHint(text))));

            int running = 0;
            foreach (var label in equationOrder)
            {
                var cost = equationCosts[label];
                running += cost;
                var rowText = $"{label} = {cost} ({running})";
                rows.Add(new ItemWalletRowData(
                    rowText,
                    ItemAbilityLinkService.TryCreateHint(label)));
            }

            rows.Add(new ItemWalletRowData(
                $"Final ISP = {item.Isp} ({item.Isp})",
                null));
            return rows;
        }

        private static bool TryParseEquation(string line, out string label, out int cost)
        {
            label = string.Empty;
            cost = 0;
            var match = EquationRegex.Match(line);
            if (!match.Success)
                return false;

            label = (match.Groups["label"].Value ?? string.Empty).Trim();
            var costText = (match.Groups["cost"].Value ?? string.Empty).Trim();
            if (label.Length == 0 || !int.TryParse(costText, out cost))
                return false;

            return true;
        }

        private static string NormalizeLine(string line)
        {
            var text = (line ?? string.Empty).Trim();
            while (text.Length > 0 && (text[0] == '|' || text[0] == '-' || text[0] == '•'))
                text = text[1..].TrimStart();
            return text;
        }

        private static bool IsHiddenLine(string line)
        {
            var lower = (line ?? string.Empty).Trim().ToLowerInvariant();
            return lower.StartsWith("type:")
                   || lower.StartsWith("created:")
                   || lower.StartsWith("isp total")
                   || lower.StartsWith("isp breakdown");
        }

        private static string BuildHeader(Item item)
        {
            var parts = new List<string>
            {
                FormatItemType(item.ItemType),
                $"ISP {item.Isp}",
                item.CreatedDate.ToString("dd MMM yyyy")
            };

            if (!string.IsNullOrWhiteSpace(item.AssignedCharacterName))
                parts.Add(item.AssignedCharacterName.Trim());

            return string.Join(" • ", parts);
        }

        private static string FormatItemType(ItemTypeEnum itemType)
        {
            var token = itemType.ToString();
            if (string.IsNullOrWhiteSpace(token))
                return "Item";

            var chars = new List<char>(token.Length + 8) { token[0] };
            for (int i = 1; i < token.Length; i++)
            {
                var c = token[i];
                if (char.IsUpper(c) && !char.IsWhiteSpace(token[i - 1]))
                    chars.Add(' ');
                chars.Add(c);
            }

            return new string(chars.ToArray());
        }

        private sealed record ItemWalletRowData(
            string Text,
            ItemAbilityLinkService.AbilityLinkHint? AbilityLinkHint);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class ItemWalletDetailRowVm : INotifyPropertyChanged
    {
        private ItemAbilityLinkService.AbilityLinkHint? _abilityLinkHint;
        private EvolutionService.AbilityResult? _resolvedAbility;

        public ItemWalletDetailRowVm(
            string text,
            Color backgroundColor,
            ItemAbilityLinkService.AbilityLinkHint? abilityLinkHint)
        {
            Text = text;
            BackgroundColor = backgroundColor;
            _abilityLinkHint = abilityLinkHint;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Text { get; }
        public Color BackgroundColor { get; }
        public bool HasInfoButton => _abilityLinkHint != null;

        public async Task<EvolutionService.AbilityResult?> ResolveAbilityAsync()
        {
            if (_resolvedAbility != null)
                return _resolvedAbility;

            if (_abilityLinkHint == null)
                return null;

            _resolvedAbility = await ItemAbilityLinkService.ResolveAbilityAsync(_abilityLinkHint);
            if (_resolvedAbility == null)
            {
                _abilityLinkHint = null;
                OnPropertyChanged(nameof(HasInfoButton));
                return null;
            }

            return _resolvedAbility;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
