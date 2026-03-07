using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages;
using labyItems.Pages.Battleboard;
using labyItems.Services;

namespace labyItems.Pages.Characters;

public partial class CharacterWalletPage : ContentPage
{
    public ObservableCollection<CharacterWalletRowVm> CharacterRows { get; } = new();
    private readonly HashSet<CharacterWalletRowVm> _animatingRows = new();

    public CharacterWalletPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadCharacters();
    }

    private void LoadCharacters()
    {
        CharacterRows.Clear();

        var list = LiteDbService.GetCharacters()
            .OrderByDescending(c => c.UpdatedUtc)
            .ToList();

        foreach (var c in list)
            CharacterRows.Add(new CharacterWalletRowVm(c));

        EmptyStateLabel.IsVisible = CharacterRows.Count == 0;
    }

    private async void OnToggleExpandedClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not CharacterWalletRowVm row)
            return;

        if (!_animatingRows.Add(row))
            return;

        try
        {
            if (!TryResolveActionContainers(sender, out var collapsedIcons, out var expandedActions))
            {
                row.IsExpanded = !row.IsExpanded;
                return;
            }

            ApplyActionButtonOrdering(collapsedIcons);
            ApplyActionButtonOrdering(expandedActions);
            await ToggleRowActionsAsync(row, collapsedIcons, expandedActions);
        }
        finally
        {
            _animatingRows.Remove(row);
        }
    }

    private async void OnSummaryClicked(object sender, EventArgs e)
    {
        var c = ResolveCharacter(sender);
        if (c != null)
        {
            await Navigation.PushAsync(new CharacterReviewPage(c));
        }
    }

    private async void OnWizardClicked(object sender, EventArgs e)
    {
        var c = ResolveCharacter(sender);
        if (c != null)
        {
            var draft = LiteDbService.ToDraft(c) ?? new CharacterDraft();
            await Navigation.PushAsync(new Wizard(draft, async () => await Navigation.PopAsync()));
        }
    }

    private async void OnAdvanceClicked(object sender, EventArgs e)
    {
        var c = ResolveCharacter(sender);
        if (c != null)
        {
            await Navigation.PushAsync(new AdvanceCharacterPage(c));
        }
    }

    private async void OnBattleboardClicked(object sender, EventArgs e)
    {
        var c = ResolveCharacter(sender);
        if (c != null)
        {
            var draft = LiteDbService.ToDraft(c) ?? new CharacterDraft();
            await Navigation.PushAsync(new BattleboardPage(draft));
        }
    }

    private async void OnManufacturingClicked(object sender, EventArgs e)
    {
        var c = ResolveCharacter(sender);
        if (c != null)
        {
            await Navigation.PushAsync(new MakeSheetPage(c));
        }
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        var c = ResolveCharacter(sender);
        if (c != null)
        {
            LiteDbService.DeleteChar(c.Id);
            LoadCharacters();
        }
    }

    private static Character? ResolveCharacter(object sender)
    {
        if (sender is Button button)
        {
            if (button.CommandParameter is Character fromButtonParam)
                return fromButtonParam;

            if (button.CommandParameter is CharacterWalletRowVm fromButtonRow)
                return fromButtonRow.Character;
        }

        if (sender is ImageButton imageButton)
        {
            if (imageButton.CommandParameter is Character fromImageButtonParam)
                return fromImageButtonParam;

            if (imageButton.CommandParameter is CharacterWalletRowVm fromImageButtonRow)
                return fromImageButtonRow.Character;
        }

        if (sender is BindableObject bindable)
        {
            if (bindable.BindingContext is Character fromContext)
                return fromContext;

            if (bindable.BindingContext is CharacterWalletRowVm fromRowContext)
                return fromRowContext.Character;
        }

        return null;
    }

    private async Task ToggleRowActionsAsync(
        CharacterWalletRowVm row,
        VisualElement collapsedIcons,
        VisualElement expandedActions)
    {
        var shouldExpand = !row.IsExpanded;
        row.IsExpanded = shouldExpand;

        if (shouldExpand)
        {
            collapsedIcons.IsVisible = true;
            collapsedIcons.Opacity = 1;
            collapsedIcons.InputTransparent = true;

            expandedActions.IsVisible = true;
            expandedActions.Opacity = 0;
            expandedActions.InputTransparent = false;

            await Task.WhenAll(
                CardExpandAnimationHelper.FadeAsync(collapsedIcons, 0),
                CardExpandAnimationHelper.FadeAsync(expandedActions, 1));
        }
        else
        {
            collapsedIcons.IsVisible = true;
            collapsedIcons.Opacity = 0;
            collapsedIcons.InputTransparent = false;

            expandedActions.IsVisible = true;
            expandedActions.Opacity = 1;
            expandedActions.InputTransparent = true;

            await Task.WhenAll(
                CardExpandAnimationHelper.FadeAsync(collapsedIcons, 1),
                CardExpandAnimationHelper.FadeAsync(expandedActions, 0));
        }

        ApplyRowVisualState(collapsedIcons, expandedActions, row.IsExpanded);
    }

    private void OnWalletCardBindingContextChanged(object sender, EventArgs e)
    {
        if (sender is not Frame frame || frame.BindingContext is not CharacterWalletRowVm row)
            return;

        var collapsedIcons = FindDescendantByStyleId<VisualElement>(frame, "wallet-collapsed-icons");
        var expandedActions = FindDescendantByStyleId<VisualElement>(frame, "wallet-expanded-actions");
        if (collapsedIcons == null || expandedActions == null)
            return;

        ApplyActionButtonOrdering(collapsedIcons);
        ApplyActionButtonOrdering(expandedActions);
        ApplyRowVisualState(collapsedIcons, expandedActions, row.IsExpanded);
    }

    private static void ApplyRowVisualState(VisualElement collapsedIcons, VisualElement expandedActions, bool isExpanded)
    {
        if (isExpanded)
        {
            collapsedIcons.IsVisible = false;
            collapsedIcons.Opacity = 0;
            collapsedIcons.InputTransparent = true;

            expandedActions.IsVisible = true;
            expandedActions.Opacity = 1;
            expandedActions.InputTransparent = false;
            return;
        }

        collapsedIcons.IsVisible = true;
        collapsedIcons.Opacity = 1;
        collapsedIcons.InputTransparent = false;

        expandedActions.IsVisible = false;
        expandedActions.Opacity = 0;
        expandedActions.InputTransparent = true;
    }

    private static bool TryResolveActionContainers(
        object sender,
        out VisualElement collapsedIcons,
        out VisualElement expandedActions)
    {
        collapsedIcons = null!;
        expandedActions = null!;

        if (sender is not Element element)
            return false;

        var card = FindAncestor<Frame>(element);
        if (card == null)
            return false;

        var collapsed = FindDescendantByStyleId<VisualElement>(card, "wallet-collapsed-icons");
        var expanded = FindDescendantByStyleId<VisualElement>(card, "wallet-expanded-actions");
        if (collapsed == null || expanded == null)
            return false;

        collapsedIcons = collapsed;
        expandedActions = expanded;
        return true;
    }

    private static TElement? FindAncestor<TElement>(Element? start)
        where TElement : Element
    {
        var current = start;
        while (current != null)
        {
            if (current is TElement match)
                return match;

            current = current.Parent;
        }

        return null;
    }

    private static TElement? FindDescendantByStyleId<TElement>(Element root, string styleId)
        where TElement : Element
    {
        foreach (var child in EnumerateChildren(root))
        {
            if (child is TElement typed
                && string.Equals(typed.StyleId, styleId, StringComparison.Ordinal))
            {
                return typed;
            }

            var nested = FindDescendantByStyleId<TElement>(child, styleId);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static IEnumerable<Element> EnumerateChildren(Element parent)
    {
        if (parent is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is Element elementChild)
                    yield return elementChild;
            }
        }

        if (parent is ContentView contentView && contentView.Content is Element content)
            yield return content;

        if (parent is Border border && border.Content is Element borderContent)
            yield return borderContent;

        if (parent is ScrollView scrollView && scrollView.Content is Element scrollContent)
            yield return scrollContent;
    }

    private static void ApplyActionButtonOrdering(VisualElement container)
    {
        if (container is not Layout layout || layout.Children.Count <= 1)
            return;

        var ordered = CharacterActionOrderHelper.GetOrderedViewsByStyleId(layout.Children);
        if (ordered.Count != layout.Children.Count)
            return;

        if (layout.Children.SequenceEqual(ordered))
            return;

        layout.Children.Clear();
        foreach (var child in ordered)
            layout.Children.Add(child);
    }

    public sealed class CharacterWalletRowVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public CharacterWalletRowVm(Character character)
        {
            Character = character;
        }

        public Character Character { get; }
        public string Name => Character.Name;
        public string Class => Character.Class;
        public string PlayerName => Character.PlayerName;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }
}
