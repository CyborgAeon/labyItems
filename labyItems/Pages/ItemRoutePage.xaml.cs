using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using labyItems.Pages.Calculator;
using labyItems.Pages.NonStandard;
using labyItems.Pages.Search;
using labyItems.Models;
using labyItems.Services;

namespace labyItems.Pages;

public partial class ItemRoutePage : ContentPage
{
    public ObservableCollection<ItemWalletCardVm> ItemWalletRows { get; } = new();

    public ItemRoutePage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshWalletButton();
        RefreshItemWallet();
    }

    private async void OnMakeCharacterClicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new labyItems.Pages.Characters.Wizard(null, async () => await Navigation.PopToRootAsync()));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Wizard failed", ex.ToString(), "OK");
        }
    }

    private async void OnCalculateIspClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new IspCalculator(0));
    }

    private async void OnCreateMpClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new MpCalculator());
    }

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new GlobalSearchPage());
    }

    private async void OnGuildSearchClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new GuildSearchPage());
    }

    private async void OnCreateNonStandardClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new NonStandardCreatePage());
    }

    private async void OnCharacterWalletClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new Characters.CharacterWalletPage());
    }

    private void OnItemWalletCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not ItemWalletCardVm tapped)
            return;

        var shouldExpand = !tapped.IsExpanded;
        foreach (var row in ItemWalletRows)
            row.IsExpanded = ReferenceEquals(row, tapped) && shouldExpand;
    }

    private void RefreshWalletButton()
    {
        try
        {
            var any = LiteDbService.GetCharacters().Any();
            if (CharacterWalletButton != null)
            {
                CharacterWalletButton.IsEnabled = any;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WALLET_BUTTON] {ex}");
            if (CharacterWalletButton != null)
            {
                CharacterWalletButton.IsEnabled = false;
            }
        }
    }

    private void RefreshItemWallet()
    {
        try
        {
            ItemWalletRows.Clear();
            var rows = LiteDbService.GetItems()
                .Take(50)
                .Select(item => new ItemWalletCardVm(item))
                .ToList();

            foreach (var row in rows)
                ItemWalletRows.Add(row);

            if (EmptyItemWalletLabel != null)
                EmptyItemWalletLabel.IsVisible = ItemWalletRows.Count == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ITEM_WALLET] {ex}");
            if (EmptyItemWalletLabel != null)
            {
                EmptyItemWalletLabel.Text = "Could not load item wallet.";
                EmptyItemWalletLabel.IsVisible = true;
            }
        }
    }

    public sealed class ItemWalletCardVm : INotifyPropertyChanged
    {
        private static readonly Color RowEvenColor = Colors.White;
        private static readonly Color RowOddColor = Color.FromArgb("#F6F6F6");
        private bool _isExpanded;

        public ItemWalletCardVm(Item item)
        {
            Item = item;
            Title = ResolveTitle(item);
            Subtitle = $"{FormatItemType(item.ItemType)} · ISP {item.Isp} · {item.CreatedDate:dd MMM yyyy}";
            DetailRows = BuildDetailRows(item);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Item Item { get; }

        public string Title { get; }

        public string Subtitle { get; }

        public ObservableCollection<ItemWalletDetailRowVm> DetailRows { get; }

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

        private static ObservableCollection<ItemWalletDetailRowVm> BuildDetailRows(Item item)
        {
            var lines = BuildLines(item);
            var rows = new ObservableCollection<ItemWalletDetailRowVm>();

            for (int i = 0; i < lines.Count; i++)
            {
                rows.Add(new ItemWalletDetailRowVm(lines[i], i % 2 == 0 ? RowEvenColor : RowOddColor));
            }

            return rows;
        }

        private static List<string> BuildLines(Item item)
        {
            var lines = new List<string>
            {
                $"Type: {FormatItemType(item.ItemType)}",
                $"Created: {item.CreatedDate:dd MMM yyyy HH:mm}"
            };

            var descriptionLines = (item.Description ?? string.Empty)
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var hasIspLine = descriptionLines.Any(line =>
                line.StartsWith("ISP", StringComparison.OrdinalIgnoreCase));
            if (!hasIspLine)
                lines.Insert(0, $"ISP total: {item.Isp}");

            lines.AddRange(descriptionLines);
            return lines;
        }

        private static string ResolveTitle(Item item)
        {
            var firstLine = (item.Description ?? string.Empty)
                .Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

            if (!string.IsNullOrWhiteSpace(firstLine))
                return firstLine;

            return $"Saved {FormatItemType(item.ItemType)} item";
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

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class ItemWalletDetailRowVm
    {
        public ItemWalletDetailRowVm(string text, Color backgroundColor)
        {
            Text = text;
            BackgroundColor = backgroundColor;
        }

        public string Text { get; }
        public Color BackgroundColor { get; }
    }
}
