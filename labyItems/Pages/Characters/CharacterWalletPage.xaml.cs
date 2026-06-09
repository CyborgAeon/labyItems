using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using labyItems.Controls;
using labyItems.Controls.Pickers;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages;
using labyItems.Pages.Battleboard;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters;

public partial class CharacterWalletPage : ContentPage
{
    public ObservableCollection<CharacterWalletRowVm> CharacterRows { get; } = new();

    private readonly HashSet<CharacterWalletRowVm> _animatingRows = new();
    private readonly IDocumentReferenceService _documentReferenceService;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private CancellationTokenSource? _loadCts;
    private int _loadVersion;
    private bool _suppressNextCardTap;

    public CharacterWalletPage()
    {
        _documentReferenceService = ServiceHelper.ResolveService<IDocumentReferenceService>() ?? new DocumentReferenceService();
        InitializeComponent();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DictionaryOverlayRegistry.DismissAll();
        InlineSuggestionsOverlay.Dismiss();
        WalletView.InputTransparent = false;
        WalletView.IsEnabled = true;
        await LoadCharactersAsync();
    }

    protected override void OnDisappearing()
    {
        CancelLoad();
        base.OnDisappearing();
    }

    private async Task LoadCharactersAsync()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _loadCts, cts);
        previous?.Cancel();

        var loadVersion = Interlocked.Increment(ref _loadVersion);
        var lockTaken = false;

        try
        {
            await _loadGate.WaitAsync(cts.Token);
            lockTaken = true;
            cts.Token.ThrowIfCancellationRequested();

            _animatingRows.Clear();
            CharacterRows.Clear();

            var rows = await Task.Run(async () =>
            {
                cts.Token.ThrowIfCancellationRequested();
                var classes = await ClassService.GetAllAsync().ConfigureAwait(false);
                cts.Token.ThrowIfCancellationRequested();
                return LiteDbService.GetCharacters()
                    .OrderByDescending(c => c.UpdatedUtc)
                    .Select(character => new CharacterWalletRowSeed(
                        character,
                        ResolveClassBrackets(character, classes)))
                    .ToArray();
            }, cts.Token);

            cts.Token.ThrowIfCancellationRequested();

            if (loadVersion != _loadVersion)
                return;

            foreach (var row in rows)
            {
                cts.Token.ThrowIfCancellationRequested();
                CharacterRows.Add(new CharacterWalletRowVm(row.Character, row.ClassBrackets));
            }

            var hasNoCharacters = CharacterRows.Count == 0;
            EmptyStateLabel.IsVisible = hasNoCharacters;
            CreateCharacterButton.IsVisible = hasNoCharacters;

            // Force row container recreation to avoid stale recycled visual/input state after deep navigation.
            WalletView.ItemsSource = null;
            WalletView.ItemsSource = CharacterRows;
            WalletView.InvalidateMeasure();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("CHARACTER_WALLET_LOAD", "Character wallet load failed.", ex);
            EmptyStateLabel.IsVisible = true;
            CreateCharacterButton.IsVisible = true;
            await DisplayAlert("Characters failed", ex.Message, "OK");
        }
        finally
        {
            if (lockTaken)
                _loadGate.Release();

            if (ReferenceEquals(_loadCts, cts))
                _loadCts = null;

            cts.Dispose();
        }
    }

    private void CancelLoad()
    {
        var cts = Interlocked.Exchange(ref _loadCts, null);
        cts?.Cancel();
    }

    private async void OnWalletCardTapped(object sender, TappedEventArgs e)
    {
        if (_suppressNextCardTap)
        {
            _suppressNextCardTap = false;
            return;
        }

        var row = ResolveWalletRow(sender);
        if (row == null)
            return;

        await ToggleExpandedAsync(sender, row);
    }

    private async Task ToggleExpandedAsync(object sender, CharacterWalletRowVm row)
    {
        if (!_animatingRows.Add(row))
            return;

        try
        {
            if (!TryResolveExpandedActions(sender, out var expandedActions))
            {
                row.IsExpanded = !row.IsExpanded;
                return;
            }

            await ToggleRowActionsAsync(row, expandedActions);
        }
        finally
        {
            _animatingRows.Remove(row);
        }
    }

    private async void OnAvatarTapped(object sender, TappedEventArgs e)
    {
        _suppressNextCardTap = true;

        try
        {
            var row = ResolveWalletRow(sender);
            if (row == null)
                return;

            var action = row.HasCustomAvatar
                ? await DisplayActionSheet("Character icon", "Cancel", "Use default icon", "Take photo", "Choose image")
                : await DisplayActionSheet("Character icon", "Cancel", null, "Take photo", "Choose image");

            if (string.Equals(action, "Use default icon", StringComparison.Ordinal))
            {
                ClearCharacterAvatar(row.Character);
                await Task.Run(() => LiteDbService.UpsertCharacter(row.Character));
                row.RefreshAvatar();
                return;
            }

            DocumentReferenceCapture? image = action switch
            {
                "Take photo" => await _documentReferenceService.CapturePhotoAsync(),
                "Choose image" => await _documentReferenceService.PickImageAsync(),
                _ => null
            };

            if (image == null)
                return;

            ApplyCharacterAvatar(row.Character, image);
            await Task.Run(() => LiteDbService.UpsertCharacter(row.Character));
            row.RefreshAvatar();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Icon update failed", ex.Message, "OK");
        }
        finally
        {
            await Task.Delay(100);
            _suppressNextCardTap = false;
        }
    }

    private async void OnActionRowClicked(object sender, EventArgs e)
    {
        _suppressNextCardTap = true;

        try
        {
            if (sender is not BindableObject bindable
                || bindable.BindingContext is not CharacterWalletActionVm action)
            {
                return;
            }

            await ExecuteActionAsync(action);
        }
        finally
        {
            await Task.Delay(100);
            _suppressNextCardTap = false;
        }
    }

    private async Task ExecuteActionAsync(CharacterWalletActionVm action)
    {
        var character = action.Character;
        switch (action.Key)
        {
            case CharacterWalletActionVm.ReviewKey:
                await Navigation.PushAsync(new CharacterReviewPage(character));
                break;

            case CharacterWalletActionVm.EditKey:
                {
                    var draft = LiteDbService.ToDraft(character) ?? new CharacterDraft();
                    await Navigation.PushAsync(new Wizard(draft, async () => await Navigation.PopAsync()));
                    break;
                }

            case CharacterWalletActionVm.AdvanceKey:
                await Navigation.PushAsync(new AdvanceCharacterPage(character));
                break;

            case CharacterWalletActionVm.BattleboardKey:
                {
                    var draft = LiteDbService.ToDraft(character) ?? new CharacterDraft();
                    await Navigation.PushAsync(new BattleboardPage(draft));
                    break;
                }

            case CharacterWalletActionVm.ExportBattleboardKey:
                await ExportBattleboardAsync(character);
                break;

            case CharacterWalletActionVm.CraftingKey:
                await Navigation.PushAsync(new MakeSheetPage(character));
                break;

            case CharacterWalletActionVm.DeleteKey:
                await Task.Run(() => LiteDbService.DeleteChar(character.Id));
                await LoadCharactersAsync();
                break;
        }
    }

    private async void OnAddCharacterClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new Wizard(null, async () => await Navigation.PopAsync()));

    private async Task ExportBattleboardAsync(Character character)
    {
        var draft = LiteDbService.ToDraft(character) ?? new CharacterDraft();
        var vm = new WizardVm(
            draft,
            runBuilderStartupPipeline: false,
            runInitialSync: false);
        await vm.RefreshReviewAsync();

        var choicesComplete = await GuildBenefitChoicePromptHelper.EnsureChoicesCompletedAsync(
            this,
            vm.GuildsVm,
            refreshAfterSelection: () => vm.RefreshReviewAsync(),
            actionLabel: "exporting the battleboard");
        if (!choicesComplete)
            return;

        await vm.DownloadBattleboardAsExcelAsync();
    }

    private async Task ToggleRowActionsAsync(CharacterWalletRowVm row, VisualElement expandedActions)
    {
        var shouldExpand = !row.IsExpanded;
        row.IsExpanded = shouldExpand;

        if (shouldExpand)
        {
            expandedActions.IsVisible = true;
            expandedActions.Opacity = 0;
            expandedActions.InputTransparent = false;
            await CardExpandAnimationHelper.FadeAsync(expandedActions, 1);
        }
        else
        {
            expandedActions.IsVisible = true;
            expandedActions.Opacity = 1;
            expandedActions.InputTransparent = true;
            await CardExpandAnimationHelper.FadeAsync(expandedActions, 0);
        }

        ApplyRowVisualState(expandedActions, row.IsExpanded);
    }

    private void OnWalletCardBindingContextChanged(object sender, EventArgs e)
    {
        if (sender is not Frame frame || frame.BindingContext is not CharacterWalletRowVm row)
            return;

        var expandedActions = FindDescendantByStyleId<VisualElement>(frame, "wallet-expanded-actions");
        if (expandedActions == null)
            return;

        ApplyRowVisualState(expandedActions, row.IsExpanded);
    }

    private static void ApplyRowVisualState(VisualElement expandedActions, bool isExpanded)
    {
        expandedActions.IsVisible = isExpanded;
        expandedActions.Opacity = isExpanded ? 1 : 0;
        expandedActions.InputTransparent = !isExpanded;
    }

    private static bool TryResolveExpandedActions(object sender, out VisualElement expandedActions)
    {
        expandedActions = null!;

        if (sender is not Element element)
            return false;

        var card = FindAncestor<Frame>(element);
        if (card == null)
            return false;

        var expanded = FindDescendantByStyleId<VisualElement>(card, "wallet-expanded-actions");
        if (expanded == null)
            return false;

        expandedActions = expanded;
        return true;
    }

    private static CharacterWalletRowVm? ResolveWalletRow(object sender)
    {
        if (sender is not BindableObject bindable)
            return null;

        return bindable.BindingContext switch
        {
            CharacterWalletRowVm row => row,
            CharacterWalletActionVm action => action.Row,
            _ => null
        };
    }

    private static IReadOnlyList<string> ResolveClassBrackets(
        Character character,
        IReadOnlyDictionary<string, CharacterClassRecord> classes)
    {
        var className = (character.Class ?? string.Empty).Trim();
        if (className.Length == 0)
            return Array.Empty<string>();

        if (classes.TryGetValue(className, out var exact))
            return exact.Brackets;

        var wanted = LifeScalesService.NormalizeKey(className);
        foreach (var pair in classes)
        {
            if (LifeScalesService.NormalizeKey(pair.Key) == wanted)
                return pair.Value.Brackets;
        }

        return Array.Empty<string>();
    }

    private static void ApplyCharacterAvatar(Character character, DocumentReferenceCapture capture)
    {
        var reference = DocumentReferenceInfo.FromCapture(capture);
        character.AvatarStorageKind = reference.StorageKind ?? string.Empty;
        character.AvatarPersistentReference = reference.PersistentReference ?? string.Empty;
        character.AvatarSourceUri = reference.SourceUri ?? string.Empty;
        character.AvatarAccessReference = reference.AccessReference ?? string.Empty;
        character.AvatarDisplayPath = reference.DisplayPath ?? string.Empty;
        character.AvatarFileName = reference.FileName ?? string.Empty;
        character.AvatarContentType = reference.ContentType ?? string.Empty;
        character.DraftSnapshot = UpdateDraftAvatarSnapshot(character);
    }

    private static void ClearCharacterAvatar(Character character)
    {
        character.AvatarStorageKind = string.Empty;
        character.AvatarPersistentReference = string.Empty;
        character.AvatarSourceUri = string.Empty;
        character.AvatarAccessReference = string.Empty;
        character.AvatarDisplayPath = string.Empty;
        character.AvatarFileName = string.Empty;
        character.AvatarContentType = string.Empty;
        character.DraftSnapshot = UpdateDraftAvatarSnapshot(character);
    }

    private static string UpdateDraftAvatarSnapshot(Character character)
    {
        var draft = LiteDbService.ToDraft(character);
        if (draft == null)
            return character.DraftSnapshot ?? string.Empty;

        draft.AvatarStorageKind = character.AvatarStorageKind ?? string.Empty;
        draft.AvatarPersistentReference = character.AvatarPersistentReference ?? string.Empty;
        draft.AvatarSourceUri = character.AvatarSourceUri ?? string.Empty;
        draft.AvatarAccessReference = character.AvatarAccessReference ?? string.Empty;
        draft.AvatarDisplayPath = character.AvatarDisplayPath ?? string.Empty;
        draft.AvatarFileName = character.AvatarFileName ?? string.Empty;
        draft.AvatarContentType = character.AvatarContentType ?? string.Empty;
        return System.Text.Json.JsonSerializer.Serialize(draft);
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

    private sealed record CharacterWalletRowSeed(Character Character, IReadOnlyList<string> ClassBrackets);

    public sealed class CharacterWalletRowVm : INotifyPropertyChanged
    {
        private readonly IReadOnlyList<string> _bracketTags;
        private ImageSource? _avatarImageSource;
        private bool _hasCustomAvatar;
        private bool _isExpanded;

        public event PropertyChangedEventHandler? PropertyChanged;

        public CharacterWalletRowVm(Character character, IReadOnlyList<string> classBrackets)
        {
            Character = character;

            var parsed = ClassBracketIconHelper.ParseBrackets(classBrackets);
            Icon = parsed.Icon;
            Category = parsed.Category;
            _bracketTags = classBrackets.Count > 0 ? classBrackets : parsed.Tags;
            ActionRows = new ObservableCollection<CharacterWalletActionVm>(BuildActionRows());
            RefreshAvatar();
        }

        public Character Character { get; }
        public ObservableCollection<CharacterWalletActionVm> ActionRows { get; }
        public string Name => Character.Name;
        public string Class => Character.Class;
        public string PlayerName => Character.PlayerName;
        public string Icon { get; }
        public string Category { get; }
        public IReadOnlyList<string> Brackets => _bracketTags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        public string BracketSubtitle => ClassBracketIconHelper.BuildBracketSubheading(Brackets, Character.Class);
        public bool HasSplitIcon => Brackets.Count >= 2;
        public string SingleGlyph => Brackets.Count == 0 ? Icon : ClassBracketIconHelper.GetBracketGlyph(Brackets[0]);
        public string SingleIconFontFamily => string.Empty;
        public Color SingleBackground => ClassBracketIconHelper.GetBracketColor(Brackets.Count == 0 ? Category : Brackets[0]);
        public string SplitLeftGlyph => ClassBracketIconHelper.GetBracketGlyph(GetBracketAt(0, Category));
        public string SplitRightGlyph => ClassBracketIconHelper.GetBracketGlyph(GetBracketAt(1, GetBracketAt(0, Category)));
        public Color SplitLeftBackground => ClassBracketIconHelper.GetBracketColor(GetBracketAt(0, Category));
        public Color SplitRightBackground => ClassBracketIconHelper.GetBracketColor(GetBracketAt(1, GetBracketAt(0, Category)));
        public string PointsChipText
        {
            get
            {
                var points = Math.Max(0, Character.Points);
                var formatted = points > 9999 ? points.ToKNotation() : points.ToString();
                return $"{formatted} pts";
            }
        }

        public string LastPlayedText => $"Last played {FormatRelativeTime(Character.UpdatedUtc)}";
        public ImageSource? AvatarImageSource => _avatarImageSource;
        public bool HasCustomAvatar => _hasCustomAvatar;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;
                Raise();
            }
        }

        public void RefreshAvatar()
        {
            var reference = BuildAvatarReference(Character);
            _hasCustomAvatar = reference?.HasReference == true;
            _avatarImageSource = DocumentReferenceImageSourceHelper.CreateImageSource(reference);
            Raise(nameof(HasCustomAvatar));
            Raise(nameof(AvatarImageSource));
            Raise(nameof(LastPlayedText));
        }

        private IEnumerable<CharacterWalletActionVm> BuildActionRows()
        {
            yield return new CharacterWalletActionVm(this, CharacterWalletActionVm.ReviewKey, FontAwesomeGlyphs.Review, FontAwesomeGlyphs.SolidFamily, "Review", false);
            yield return new CharacterWalletActionVm(this, CharacterWalletActionVm.EditKey, FontAwesomeGlyphs.Edit, FontAwesomeGlyphs.SolidFamily, "Edit", false);
            yield return new CharacterWalletActionVm(this, CharacterWalletActionVm.AdvanceKey, FontAwesomeGlyphs.Advance, FontAwesomeGlyphs.SolidFamily, "Advance", false);
            yield return new CharacterWalletActionVm(this, CharacterWalletActionVm.BattleboardKey, FontAwesomeGlyphs.Battleboard, string.Empty, "Battleboard", false);
            yield return new CharacterWalletActionVm(this, CharacterWalletActionVm.ExportBattleboardKey, FontAwesomeGlyphs.ExportBattleboard, FontAwesomeGlyphs.SolidFamily, "Export battleboard", false);
            yield return new CharacterWalletActionVm(this, CharacterWalletActionVm.CraftingKey, FontAwesomeGlyphs.Crafting, FontAwesomeGlyphs.SolidFamily, "Crafting", false);
            yield return new CharacterWalletActionVm(this, CharacterWalletActionVm.DeleteKey, FontAwesomeGlyphs.Delete, FontAwesomeGlyphs.SolidFamily, "Delete", true);
        }

        private string GetBracketAt(int index, string fallback)
        {
            if (index >= 0 && index < Brackets.Count)
                return Brackets[index];

            return fallback;
        }

        private static DocumentReferenceInfo? BuildAvatarReference(Character character)
        {
            var reference = new DocumentReferenceInfo(
                character.AvatarStorageKind ?? string.Empty,
                character.AvatarPersistentReference ?? string.Empty,
                string.IsNullOrWhiteSpace(character.AvatarSourceUri) ? null : character.AvatarSourceUri,
                string.IsNullOrWhiteSpace(character.AvatarAccessReference) ? null : character.AvatarAccessReference,
                string.IsNullOrWhiteSpace(character.AvatarDisplayPath) ? null : character.AvatarDisplayPath,
                string.IsNullOrWhiteSpace(character.AvatarFileName) ? null : character.AvatarFileName,
                string.IsNullOrWhiteSpace(character.AvatarContentType) ? null : character.AvatarContentType);

            return reference.HasReference ? reference : null;
        }

        private static string FormatRelativeTime(DateTime timestamp)
        {
            var normalized = timestamp.Kind == DateTimeKind.Utc
                ? timestamp
                : timestamp.ToUniversalTime();
            var span = DateTime.UtcNow - normalized;

            if (span.TotalDays >= 2)
                return $"{Math.Floor(span.TotalDays)}d ago";
            if (span.TotalDays >= 1)
                return "1d ago";
            if (span.TotalHours >= 2)
                return $"{Math.Floor(span.TotalHours)}h ago";
            if (span.TotalHours >= 1)
                return "1h ago";
            if (span.TotalMinutes >= 2)
                return $"{Math.Floor(span.TotalMinutes)}m ago";

            return "just now";
        }

        private void Raise([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class CharacterWalletActionVm
    {
        public const string ReviewKey = "review";
        public const string EditKey = "edit";
        public const string AdvanceKey = "advance";
        public const string BattleboardKey = "battleboard";
        public const string ExportBattleboardKey = "export-battleboard";
        public const string CraftingKey = "crafting";
        public const string DeleteKey = "delete";

        public CharacterWalletActionVm(
            CharacterWalletRowVm row,
            string key,
            string iconGlyph,
            string iconFontFamily,
            string title,
            bool isDestructive)
        {
            Row = row;
            Key = key;
            IconGlyph = iconGlyph;
            IconFontFamily = iconFontFamily;
            Title = title;
            IsDestructive = isDestructive;
        }

        public CharacterWalletRowVm Row { get; }
        public Character Character => Row.Character;
        public string Key { get; }
        public string IconGlyph { get; }
        public string IconFontFamily { get; }
        public string Title { get; }
        public bool IsDestructive { get; }
        public Color TextColor => IsDestructive ? Color.FromArgb("#B91C1C") : Color.FromArgb("#141414");
        public string ChevronGlyph => FontAwesomeGlyphs.Chevron;
    }
}
