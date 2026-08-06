using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages;
using labyItems.Pages.Battleboard;
using labyItems.Pages.Calculator;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Pages.ItemCard;
using labyItems.Services;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewPage : ContentPage
{
    private readonly WizardVm _vm;
    private readonly AdvanceCharacterVm _advanceVm;
    private readonly CharacterDraft _draft;
    private readonly Character _character;
    private readonly IDocumentReferenceService _documentReferenceService;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CancellationTokenSource? _refreshCts;
    private bool _isNavigatingBack;
    private bool _isActionMenuExpanded;
    private bool _isActionMenuAnimating;
    private bool _isSectionAnimating;
    private string _selectedReviewSection = ReviewSectionKeys.Details;

    public ObservableCollection<ReviewSectionVm> AvailableReviewSections { get; } = new();
    public ObservableCollection<ReviewGroupVm> SpellReviewGroups { get; } = new();
    public ObservableCollection<ReviewGroupVm> MiracleReviewGroups { get; } = new();
    public ObservableCollection<ReviewGroupVm> EvocationReviewGroups { get; } = new();
    public ObservableCollection<ReviewGroupVm> NeuronicReviewGroups { get; } = new();
    public ObservableCollection<ReviewEntryVm> ItemReviewEntries { get; } = new();

    public bool IsDetailsSelected => _selectedReviewSection == ReviewSectionKeys.Details;
    public bool IsSpellsSelected => _selectedReviewSection == ReviewSectionKeys.Spells;
    public bool IsMiraclesSelected => _selectedReviewSection == ReviewSectionKeys.Miracles;
    public bool IsEvocationsSelected => _selectedReviewSection == ReviewSectionKeys.Evocations;
    public bool IsNeuronicsSelected => _selectedReviewSection == ReviewSectionKeys.Neuronics;
    public bool IsItemsSelected => _selectedReviewSection == ReviewSectionKeys.Items;

    public bool HasSpellReviewGroups => SpellReviewGroups.Count > 0;
    public bool HasMiracleReviewGroups => MiracleReviewGroups.Count > 0;
    public bool HasEvocationReviewGroups => EvocationReviewGroups.Count > 0;
    public bool HasNeuronicReviewGroups => NeuronicReviewGroups.Count > 0;
    public bool HasItemReviewEntries => ItemReviewEntries.Count > 0;

    public CharacterReviewPage(Character character)
    {
        InitializeComponent();
        ApplyActionButtonOrdering(ExpandedActionsPanel);

        _character = character;
        _draft = LiteDbService.ToDraft(character) ?? new CharacterDraft();
        _documentReferenceService = ServiceHelper.ResolveService<IDocumentReferenceService>() ?? new DocumentReferenceService();
        _vm = new WizardVm(
            _draft,
            runBuilderStartupPipeline: false,
            runInitialSync: false);

        var serviceProvider = Application.Current?.Handler?.MauiContext?.Services
            ?? throw new InvalidOperationException("Service provider is not available.");
        _advanceVm = new AdvanceCharacterVm(
            _draft,
            draftStore: new CharacterDraftStore(_draft),
            domainService: serviceProvider.GetRequiredService<ICharacterAdvancementDomainService>(),
            tabVisibilityService: serviceProvider.GetRequiredService<IAdvancementTabVisibilityService>(),
            validationService: serviceProvider.GetRequiredService<IAdvancementValidationService>(),
            exportService: serviceProvider.GetRequiredService<IExportService>(),
            fileService: serviceProvider.GetRequiredService<IFileService>(),
            dataProvider: serviceProvider.GetRequiredService<IAdvanceCharacterDataProvider>(),
            abilityLookupService: serviceProvider.GetRequiredService<IAdvanceAbilityLookupService>(),
            abilityAvailabilityService: serviceProvider.GetRequiredService<IAbilityAvailabilityService>());

        Title = string.IsNullOrWhiteSpace(_draft.Name) ? "Character" : _draft.Name;
        BindingContext = this;
        DetailsSectionView.BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshReviewSafelyAsync();
    }

    protected override void OnDisappearing()
    {
        CancelRefresh();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_isActionMenuExpanded)
        {
            _ = CollapseActionMenuAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (_isNavigatingBack)
            return;

        _isNavigatingBack = true;
        try
        {
            await CloseActionMenuIfOpenAsync();

            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isNavigatingBack = false;
        }
    }

    private async void OnOverflowTapped(object sender, EventArgs e)
    {
        if (_isActionMenuExpanded)
            await CollapseActionMenuAsync();
        else
            await ExpandActionMenuAsync();
    }

    private async void OnActionBackdropTapped(object sender, TappedEventArgs e)
    {
        await CollapseActionMenuAsync();
    }

    private async Task ExpandActionMenuAsync()
    {
        if (_isActionMenuAnimating || _isActionMenuExpanded)
            return;

        _isActionMenuAnimating = true;
        try
        {
            _isActionMenuExpanded = true;
            ActionBackdrop.IsVisible = true;
            ActionBackdrop.InputTransparent = false;
            ExpandedActionsPanel.IsVisible = true;
            ExpandedActionsPanel.Opacity = 0;
            ExpandedActionsPanel.TranslationY = -8;

            await Task.WhenAll(
                CardExpandAnimationHelper.FadeAsync(ActionBackdrop, 1),
                CardExpandAnimationHelper.FadeAsync(ExpandedActionsPanel, 1),
                CardExpandAnimationHelper.TranslateYAsync(ExpandedActionsPanel, 0));
        }
        finally
        {
            _isActionMenuAnimating = false;
        }
    }

    private async Task CollapseActionMenuAsync()
    {
        if (_isActionMenuAnimating || !_isActionMenuExpanded)
            return;

        _isActionMenuAnimating = true;
        try
        {
            _isActionMenuExpanded = false;

            await Task.WhenAll(
                CardExpandAnimationHelper.FadeAsync(ActionBackdrop, 0),
                CardExpandAnimationHelper.FadeAsync(ExpandedActionsPanel, 0),
                CardExpandAnimationHelper.TranslateYAsync(ExpandedActionsPanel, -8));

            ActionBackdrop.IsVisible = false;
            ActionBackdrop.InputTransparent = true;
            ExpandedActionsPanel.IsVisible = false;
            ExpandedActionsPanel.TranslationY = 0;
        }
        finally
        {
            _isActionMenuAnimating = false;
        }
    }

    private async Task CloseActionMenuIfOpenAsync()
    {
        if (_isActionMenuExpanded)
            await CollapseActionMenuAsync();
    }

    private VisualElement? ResolveSectionView(string key)
    {
        return key switch
        {
            ReviewSectionKeys.Details => DetailsSectionView,
            ReviewSectionKeys.Spells => SpellsSectionView,
            ReviewSectionKeys.Miracles => MiraclesSectionView,
            ReviewSectionKeys.Evocations => EvocationsSectionView,
            ReviewSectionKeys.Neuronics => null,
            ReviewSectionKeys.Items => ItemsSectionView,
            _ => null
        };
    }

    private void ApplySelectedSectionVisibility(bool forceImmediate)
    {
        var details = ResolveSectionView(ReviewSectionKeys.Details);
        var spells = ResolveSectionView(ReviewSectionKeys.Spells);
        var miracles = ResolveSectionView(ReviewSectionKeys.Miracles);
        var evocations = ResolveSectionView(ReviewSectionKeys.Evocations);
        var items = ResolveSectionView(ReviewSectionKeys.Items);

        var allSections = new[] { details, spells, miracles, evocations, items };
        foreach (var section in allSections)
        {
            if (section == null)
                continue;

            var isSelected = ReferenceEquals(section, ResolveSectionView(_selectedReviewSection));
            section.IsVisible = isSelected;
            if (forceImmediate)
            {
                section.Opacity = isSelected ? 1 : 0;
                section.TranslationX = 0;
                section.InputTransparent = !isSelected;
            }
        }
    }

    private async Task SwitchSectionAsync(string newSection)
    {
        if (_isSectionAnimating)
            return;

        var currentView = ResolveSectionView(_selectedReviewSection);
        var nextView = ResolveSectionView(newSection);
        if (nextView == null)
            return;

        _isSectionAnimating = true;
        try
        {
            var fromKey = _selectedReviewSection;
            _selectedReviewSection = newSection;
            ApplySelectedSectionStyling();
            RaiseReviewSectionProperties();

            if (ReferenceEquals(currentView, nextView) || currentView == null)
            {
                ApplySelectedSectionVisibility(forceImmediate: true);
                return;
            }

            var moveRight = CompareSectionOrder(newSection, fromKey) > 0;
            var travel = Math.Max(44, ReviewSectionHost?.Width > 0 ? ReviewSectionHost.Width * 0.14 : 64);

            nextView.IsVisible = true;
            nextView.InputTransparent = true;
            nextView.Opacity = 0;
            nextView.TranslationX = moveRight ? travel : -travel;

            currentView.InputTransparent = true;

            await Task.WhenAll(
                CardExpandAnimationHelper.FadeAsync(currentView, 0),
                TranslateXAsync(currentView, moveRight ? -travel : travel),
                CardExpandAnimationHelper.FadeAsync(nextView, 1),
                TranslateXAsync(nextView, 0));

            currentView.IsVisible = false;
            currentView.Opacity = 1;
            currentView.TranslationX = 0;
            nextView.InputTransparent = false;
        }
        finally
        {
            _isSectionAnimating = false;
        }
    }

    private static int CompareSectionOrder(string a, string b)
        => ResolveSectionOrder(a).CompareTo(ResolveSectionOrder(b));

    private static int ResolveSectionOrder(string key)
    {
        return key switch
        {
            ReviewSectionKeys.Details => 0,
            ReviewSectionKeys.Spells => 1,
            ReviewSectionKeys.Miracles => 2,
            ReviewSectionKeys.Evocations => 3,
            ReviewSectionKeys.Items => 4,
            _ => 99
        };
    }

    private static Task TranslateXAsync(VisualElement target, double to)
        => target.TranslateTo(to, target.TranslationY, CardExpandAnimationHelper.UnifiedDurationMs, CardExpandAnimationHelper.UnifiedEasing);

    private async Task RefreshReviewSafelyAsync()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _refreshCts, cts);
        previous?.Cancel();

        var lockTaken = false;
        try
        {
            await _refreshGate.WaitAsync(cts.Token);
            lockTaken = true;
            cts.Token.ThrowIfCancellationRequested();

            await _vm.RefreshReviewAsync(cts.Token);
            await _advanceVm.InitializeAsync().ConfigureAwait(false);
            cts.Token.ThrowIfCancellationRequested();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BuildReviewSections();
                BuildReviewGroups();
                RaiseReviewSectionProperties();
                ApplySelectedSectionVisibility(forceImmediate: true);
            });

            await ApplyProfileHeaderStateAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("CHARACTER_REVIEW_REFRESH", "Character review refresh failed.", ex);
            await DisplayAlert("Review failed", ex.Message, "OK");
        }
        finally
        {
            if (lockTaken)
                _refreshGate.Release();

            if (ReferenceEquals(_refreshCts, cts))
                _refreshCts = null;

            cts.Dispose();
        }
    }

    private void CancelRefresh()
    {
        var cts = Interlocked.Exchange(ref _refreshCts, null);
        cts?.Cancel();
    }

    private void BuildReviewSections()
    {
        var sections = new List<ReviewSectionVm>
        {
            new(ReviewSectionKeys.Details, "Details")
        };

        if (_advanceVm.ShowSpellsTab)
            sections.Add(new ReviewSectionVm(ReviewSectionKeys.Spells, "Spells"));
        if (_advanceVm.ShowMiraclesTab)
            sections.Add(new ReviewSectionVm(ReviewSectionKeys.Miracles, "Miracles"));
        if (_advanceVm.ShowEvocationsTab)
            sections.Add(new ReviewSectionVm(ReviewSectionKeys.Evocations, "Evocations"));

        sections.Add(new ReviewSectionVm(ReviewSectionKeys.Items, "Items"));

        AvailableReviewSections.Clear();
        foreach (var section in sections)
            AvailableReviewSections.Add(section);

        if (!AvailableReviewSections.Any(section => section.Key == _selectedReviewSection))
            _selectedReviewSection = ReviewSectionKeys.Details;

        ApplySelectedSectionStyling();
    }

    private void BuildReviewGroups()
    {
        SpellReviewGroups.Clear();
        foreach (var spellList in _draft.SpellLists.Where(list => list.Entries.Count > 0))
        {
            var title = spellList.IsBaseList
                ? (string.IsNullOrWhiteSpace(spellList.Name) ? "Base List" : spellList.Name)
                : "Teaching Scrolls";
            var subtitle = spellList.IsBaseList ? "Base List" : "Teaching Scrolls";

            SpellReviewGroups.Add(new ReviewGroupVm(
                title,
                subtitle,
                spellList.Entries
                    .OrderBy(entry => entry.Level)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => new ReviewEntryVm(
                        ReviewEntryKind.Spell,
                        entry.Name,
                        $"Level {entry.Level} · {entry.Colour}{(entry.IsAdvanced ? " · Advanced" : string.Empty)}",
                        detailKey: entry.Name))
                    .ToList()));
        }

        MiracleReviewGroups.Clear();
        foreach (var miracleList in _draft.MiracleLists.Where(list => list.Entries.Count > 0))
        {
            var title = miracleList.IsScriptures
                ? "Scriptures of Faith"
                : (string.IsNullOrWhiteSpace(miracleList.Name) ? "Base List" : miracleList.Name);
            var subtitle = miracleList.IsScriptures
                ? "Scriptures of Faith"
                : "Base List";

            MiracleReviewGroups.Add(new ReviewGroupVm(
                title,
                subtitle,
                miracleList.Entries
                    .OrderBy(entry => entry.Power)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => new ReviewEntryVm(
                        ReviewEntryKind.Miracle,
                        entry.Name,
                        $"P{entry.Power} · {entry.Alignment} · {entry.Sphere}{(entry.IsAdvanced ? " · Advanced" : string.Empty)}",
                        detailKey: entry.Name))
                    .ToList()));
        }

        if (_draft.EvilStairwayList?.Entries?.Count > 0)
        {
            MiracleReviewGroups.Add(new ReviewGroupVm(
                string.IsNullOrWhiteSpace(_draft.EvilStairwayList.Name) ? "Evil Stairway" : _draft.EvilStairwayList.Name,
                string.Empty,
                _draft.EvilStairwayList.Entries
                    .OrderBy(entry => entry.Power)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => new ReviewEntryVm(
                        ReviewEntryKind.Miracle,
                        entry.Name,
                        $"P{entry.Power} · {entry.Alignment} · {entry.Sphere}{(entry.IsAdvanced ? " · Advanced" : string.Empty)}",
                        detailKey: entry.Name))
                    .ToList()));
        }

        EvocationReviewGroups.Clear();
        foreach (var evocationList in _draft.EvocationLists.Where(list => list.Entries.Count > 0))
        {
            var title = evocationList.IsPost8th ? "Learned" : "Base List";
            var subtitle = evocationList.IsPost8th ? "Learned" : "Base List";

            EvocationReviewGroups.Add(new ReviewGroupVm(
                title,
                subtitle,
                evocationList.Entries
                    .OrderBy(entry => entry.Power)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => new ReviewEntryVm(
                        ReviewEntryKind.Evocation,
                        entry.Name,
                        $"P{entry.Power}{(entry.IsAdvanced ? " · Advanced" : string.Empty)}",
                        detailKey: entry.Name))
                    .ToList()));
        }

        NeuronicReviewGroups.Clear();
        if (_draft.PowerPools.Count > 0)
        {
            NeuronicReviewGroups.Add(new ReviewGroupVm(
                "Power Pools",
                string.Empty,
                _draft.PowerPools
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => new ReviewEntryVm(
                        ReviewEntryKind.Neuronic,
                        pair.Key,
                        $"{pair.Value} points",
                        detailKey: pair.Key))
                    .ToList()));
        }

        ItemReviewEntries.Clear();
        foreach (var item in _character.ItemTemplates ?? new List<Item>())
        {
            ItemReviewEntries.Add(new ReviewEntryVm(
                ReviewEntryKind.Item,
                ItemDisplayHelper.BuildDisplayName(item),
                string.Empty,
                item: item));
        }

        if (ItemReviewEntries.Count == 0)
        {
            foreach (var name in _draft.AdvancementItems.Where(name => !string.IsNullOrWhiteSpace(name)))
                ItemReviewEntries.Add(new ReviewEntryVm(ReviewEntryKind.Item, name.Trim(), string.Empty));
        }

        OnPropertyChanged(nameof(HasSpellReviewGroups));
        OnPropertyChanged(nameof(HasMiracleReviewGroups));
        OnPropertyChanged(nameof(HasEvocationReviewGroups));
        OnPropertyChanged(nameof(HasNeuronicReviewGroups));
        OnPropertyChanged(nameof(HasItemReviewEntries));
    }

    private bool HasNeuronicAccess()
    {
        return false;
    }

    private void ApplySelectedSectionStyling()
    {
        foreach (var section in AvailableReviewSections)
        {
            section.IsSelected = section.Key == _selectedReviewSection;
            section.BackgroundColor = section.IsSelected ? Color.FromArgb("#FFF7ED") : Colors.White;
            section.BorderColor = section.IsSelected ? Color.FromArgb("#7F1D1D") : Color.FromArgb("#E5E7EB");
            section.TextColor = section.IsSelected ? Color.FromArgb("#7F1D1D") : Color.FromArgb("#374151");
        }
    }

    private void RaiseReviewSectionProperties()
    {
        OnPropertyChanged(nameof(IsDetailsSelected));
        OnPropertyChanged(nameof(IsSpellsSelected));
        OnPropertyChanged(nameof(IsMiraclesSelected));
        OnPropertyChanged(nameof(IsEvocationsSelected));
        OnPropertyChanged(nameof(IsNeuronicsSelected));
        OnPropertyChanged(nameof(IsItemsSelected));
    }

    private void OnReviewSectionTapped(object sender, TappedEventArgs e)
    {
        ReviewSectionVm? section = null;
        if (sender is BindableObject bindable && bindable.BindingContext is ReviewSectionVm boundSection)
            section = boundSection;
        else if (sender is TapGestureRecognizer recognizer && recognizer.CommandParameter is ReviewSectionVm commandSection)
            section = commandSection;

        if (section == null || _selectedReviewSection == section.Key)
            return;

        _ = SwitchSectionAsync(section.Key);
    }

    private async void OnReviewEntryInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ReviewEntryVm entry)
            return;

        switch (entry.Kind)
        {
            case ReviewEntryKind.Spell:
            {
                var spell = (await SpellService.GetAllAsync())
                    .FirstOrDefault(item => string.Equals(item.name, entry.DetailKey, StringComparison.OrdinalIgnoreCase));
                if (spell != null)
                    await Navigation.PushAsync(new SpellCardPage(spell));
                break;
            }
            case ReviewEntryKind.Miracle:
            {
                var miracle = (await MiracleService.GetAllAsync())
                    .FirstOrDefault(item => string.Equals(item.name, entry.DetailKey, StringComparison.OrdinalIgnoreCase));
                if (miracle != null)
                    await Navigation.PushAsync(new MiracleCardPage(miracle));
                break;
            }
            case ReviewEntryKind.Evocation:
            {
                var evocation = (await DruidEvocationService.GetAllAsync())
                    .FirstOrDefault(item => string.Equals(item.name, entry.DetailKey, StringComparison.OrdinalIgnoreCase));
                if (evocation != null)
                    await Navigation.PushAsync(new EvocationCardPage(evocation));
                break;
            }
            case ReviewEntryKind.Item when entry.Item != null:
                await Navigation.PushAsync(new ItemDetailCardPage(entry.Item));
                break;
            case ReviewEntryKind.Neuronic:
                await DisplayAlert(entry.PrimaryText, entry.SecondaryText.Length == 0 ? "No further details available." : entry.SecondaryText, "OK");
                break;
        }
    }

    private async void OnAddMpItemClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new MpCalculator());
    }

    private async void OnAddIspItemClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new IspCalculator(0));
    }

    private async void OnEditPortraitTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var action = await DisplayActionSheet(
                "Portrait",
                "Cancel",
                null,
                "Take photo",
                "Choose image",
                "Use default icon");

            if (string.IsNullOrWhiteSpace(action) || string.Equals(action, "Cancel", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.Equals(action, "Use default icon", StringComparison.Ordinal))
            {
                ClearCharacterAvatar(_character);
                await Task.Run(() => LiteDbService.UpsertCharacter(_character));
                await ApplyProfileHeaderStateAsync(CancellationToken.None);
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

            ApplyCharacterAvatar(_character, image);
            await Task.Run(() => LiteDbService.UpsertCharacter(_character));
            await ApplyProfileHeaderStateAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Portrait update failed", ex.Message, "OK");
        }
    }

    private async void OnExportExcelClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();

        var choicesComplete = await GuildBenefitChoicePromptHelper.EnsureChoicesCompletedAsync(
            this,
            _vm.GuildsVm,
            refreshAfterSelection: () => _vm.RefreshReviewAsync(),
            actionLabel: "exporting the battleboard");
        if (!choicesComplete)
            return;

        await _vm.DownloadBattleboardAsExcelAsync();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();
        await Navigation.PushAsync(new Wizard(_draft, async () => await Navigation.PopAsync()));
    }

    private async void OnAdvanceClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();
        await Navigation.PushAsync(new AdvanceCharacterPage(_draft));
    }

    private async void OnBattleboardClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();

        var choicesComplete = await GuildBenefitChoicePromptHelper.EnsureChoicesCompletedAsync(
            this,
            _vm.GuildsVm,
            refreshAfterSelection: () => _vm.RefreshReviewAsync(),
            actionLabel: "opening the battleboard");
        if (!choicesComplete)
            return;

        await NavigateAwayFromSummaryAsync(new BattleboardPage(_draft));
    }

    private async void OnManufacturingClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();
        await NavigateAwayFromSummaryAsync(new MakeSheetPage(_character));
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();

        var name = string.IsNullOrWhiteSpace(_character.Name) ? "this character" : _character.Name;
        var confirmed = await DisplayAlert(
            "Delete character",
            $"Delete {name}?",
            "Delete",
            "Cancel");
        if (!confirmed)
            return;

        await Task.Run(() => LiteDbService.DeleteChar(_character.Id));
        await NavigateBackAfterDeleteAsync();
    }

    private void OnBackToWalletClicked(object sender, EventArgs e)
    {
        OnBackClicked(sender, e);
    }

    private async Task NavigateAwayFromSummaryAsync(Page destination)
    {
        await Navigation.PushAsync(destination);
        if (Navigation.NavigationStack.Contains(this))
            Navigation.RemovePage(this);
    }

    private async Task NavigateBackAfterDeleteAsync()
    {
        if (Navigation.NavigationStack.Contains(this) && Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
            return;
        }

        if (Shell.Current != null)
            await Shell.Current.GoToAsync("..");
    }

    private async Task ApplyProfileHeaderStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var classBrackets = await ResolveClassBracketsAsync(_character, cancellationToken);
        var parsed = ClassBracketIconHelper.ParseBrackets(classBrackets);

        var portraitReference = BuildAvatarReference(_character);
        var portraitSource = DocumentReferenceImageSourceHelper.CreateImageSource(portraitReference);

        var hasSplitIcon = classBrackets.Count >= 2;
        var singleGlyph = classBrackets.Count == 0 ? parsed.Icon : ClassBracketIconHelper.GetBracketGlyph(classBrackets[0]);
        var splitLeftKey = classBrackets.Count > 0 ? classBrackets[0] : parsed.Category;
        var splitRightKey = classBrackets.Count > 1 ? classBrackets[1] : splitLeftKey;

        var points = Math.Max(0, _draft.Points);
        var table = CharacterProgressionTables.GetHighestTableReached(points);
        var currentThreshold = CharacterProgressionTables.GetPointsThresholdForTable(table);
        var nextTable = Math.Min(CharacterProgressionTables.MaxTable, table + 1);
        var nextThreshold = CharacterProgressionTables.GetPointsThresholdForTable(nextTable);
        var span = Math.Max(1, nextThreshold - currentThreshold);
        var rawProgress = (points - currentThreshold) / (double)span;
        var clampedProgress = Math.Max(0, Math.Min(1, rawProgress));

        var remainingText = nextThreshold == int.MaxValue
            ? $"{points:n0} Points"
            : $"{Math.Max(0, nextThreshold - points):n0} Points Remaining";

        var raceName = string.IsNullOrWhiteSpace(_draft.Race) ? "Race not set" : _draft.Race.Trim();
        var subtype = (_draft.RaceSubtypeValue ?? _draft.RaceSubtype ?? string.Empty).Trim();
        var alignment = _draft.Alignment;
        var alignmentMoralLabel = ResolveMoralLabel(alignment);
        var alignmentMoralGlyph = ResolveMoralGlyph(alignment);
        var summaryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_draft.Class))
            summaryParts.Add(_draft.Class.Trim());
        foreach (var multiClass in _draft.MultiClassLevels
                     .Where(pair => pair.Value > 0 && !string.IsNullOrWhiteSpace(pair.Key))
                     .Select(pair => pair.Key.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            summaryParts.Add(multiClass);
        }
        var nameText = string.IsNullOrWhiteSpace(_draft.Name) ? "Unnamed Character" : _draft.Name.Trim();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ProfilePortrait.AvatarImageSource = portraitSource;
            ProfilePortrait.HasSplitIcon = hasSplitIcon;
            ProfilePortrait.SingleGlyph = singleGlyph;
            ProfilePortrait.SingleIconFontFamily = string.Empty;
            ProfilePortrait.SingleBackground = ClassBracketIconHelper.GetBracketColor(splitLeftKey);
            ProfilePortrait.SplitLeftGlyph = ClassBracketIconHelper.GetBracketGlyph(splitLeftKey);
            ProfilePortrait.SplitRightGlyph = ClassBracketIconHelper.GetBracketGlyph(splitRightKey);
            ProfilePortrait.SplitLeftBackground = ClassBracketIconHelper.GetBracketColor(splitLeftKey);
            ProfilePortrait.SplitRightBackground = ClassBracketIconHelper.GetBracketColor(splitRightKey);

            ProfileNameLabel.Text = nameText;
            ProfileMetaLabel.Text = string.Join(" · ", summaryParts);
            ProfileRaceLabel.Text = raceName;
            ProfileSubtypeChip.IsVisible = subtype.Length > 0;
            ProfileSubtypeLabel.Text = subtype;
            AlignmentMoralGlyphLabel.Text = alignmentMoralGlyph;
            AlignmentMoralTextLabel.Text = alignmentMoralLabel;
            CurrentTableLabel.Text = $"Table {table}";
            TableProgressBar.Progress = clampedProgress;
            TableProgressTextLabel.Text = remainingText;

            ApplyAlignmentTrack(alignment);
        });
    }

    private async Task<IReadOnlyList<string>> ResolveClassBracketsAsync(Character character, CancellationToken cancellationToken)
    {
        var classes = await ClassService.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

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

    private static string ResolveMoralLabel(Alignment? alignment)
    {
        if (!alignment.HasValue)
            return "Neutral";
        var resolvedMoral = alignment.Value.Moral == MoralAxis.Neutral && alignment.Value.Order == OrderAxis.Neutral
            ? "True"
            : alignment.Value.Moral.ToString();
        return $"{alignment.Value.Order} {resolvedMoral}"; 
    }

    private static string ResolveMoralGlyph(Alignment? alignment)
    {
        if (!alignment.HasValue)
            return "\uf24e";

        return alignment.Value.Moral switch
        {
            MoralAxis.Good => "\uf515",
            MoralAxis.Evil => "\uf516",
            _ => "\uf24e"
        };
    }

    private void ApplyAlignmentTrack(Alignment? alignment)
    {
        var nodes = new[] { AlignNode0, AlignNode1, AlignNode2, AlignNode3, AlignNode4, AlignNode5, AlignNode6, AlignNode7, AlignNode8 };
        foreach (var node in nodes)
        {
            node.WidthRequest = 14;
            node.HeightRequest = 14;
            node.StrokeThickness = 0;
            node.BackgroundColor = Colors.White;
            node.Content = null;
        }

        if (!alignment.HasValue)
            return;

        var idx = ResolveAlignmentTrackIndex(alignment.Value);
        if (idx < 0 || idx >= nodes.Length)
            return;

        var selected = nodes[idx];
        selected.WidthRequest = 24;
        selected.HeightRequest = 24;
        var accentColor = Colors.White;
        selected.Stroke = new SolidColorBrush(Colors.White);
        selected.StrokeThickness = 2;
        selected.BackgroundColor = Color.FromRgba(accentColor.Red, accentColor.Green, accentColor.Blue, 0.22f);
        selected.Content = new Label
        {
            Text = ResolveMoralGlyph(alignment),
            FontFamily = "FASolid",
            FontSize = 11,
            TextColor = accentColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
    }

    private static int ResolveAlignmentTrackIndex(Alignment alignment)
    {
        if (alignment.Order == OrderAxis.Chaotic && alignment.Moral == MoralAxis.Evil) return 0;
        if (alignment.Order == OrderAxis.Neutral && alignment.Moral == MoralAxis.Evil) return 1;
        if (alignment.Order == OrderAxis.Lawful && alignment.Moral == MoralAxis.Evil) return 2;
        if (alignment.Order == OrderAxis.Chaotic && alignment.Moral == MoralAxis.Neutral) return 3;
        if (alignment.Order == OrderAxis.Neutral && alignment.Moral == MoralAxis.Neutral) return 4;
        if (alignment.Order == OrderAxis.Lawful && alignment.Moral == MoralAxis.Neutral) return 5;
        if (alignment.Order == OrderAxis.Chaotic && alignment.Moral == MoralAxis.Good) return 6;
        if (alignment.Order == OrderAxis.Neutral && alignment.Moral == MoralAxis.Good) return 7;
        if (alignment.Order == OrderAxis.Lawful && alignment.Moral == MoralAxis.Good) return 8;
        return -1;
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

    private static void ApplyActionButtonOrdering(Layout layout)
    {
        if (layout.Children.Count <= 1)
            return;

        var ordered = CharacterActionOrderHelper.GetOrderedViewsByStyleId(layout.Children);
        layout.Children.Clear();
        foreach (var child in ordered)
            layout.Children.Add(child);
    }

    public sealed class ReviewSectionVm
    {
        public ReviewSectionVm(string key, string title)
        {
            Key = key;
            Title = title;
        }

        public string Key { get; }
        public string Title { get; }
        public bool IsSelected { get; set; }
        public Color BackgroundColor { get; set; } = Colors.White;
        public Color BorderColor { get; set; } = Color.FromArgb("#E5E7EB");
        public Color TextColor { get; set; } = Color.FromArgb("#374151");
    }

    public sealed class ReviewGroupVm
    {
        public ReviewGroupVm(string title, string subtitle, IReadOnlyList<ReviewEntryVm> entries)
        {
            Title = title;
            Subtitle = subtitle;
            Entries = entries;
        }

        public string Title { get; }
        public string Subtitle { get; }
        public IReadOnlyList<ReviewEntryVm> Entries { get; }
        public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    }

    public sealed class ReviewEntryVm
    {
        public ReviewEntryVm(ReviewEntryKind kind, string primaryText, string secondaryText, string? detailKey = null, Item? item = null)
        {
            Kind = kind;
            PrimaryText = primaryText;
            SecondaryText = secondaryText;
            DetailKey = (detailKey ?? string.Empty).Trim();
            Item = item;
        }

        public ReviewEntryKind Kind { get; }
        public string PrimaryText { get; }
        public string SecondaryText { get; }
        public string DetailKey { get; }
        public Item? Item { get; }
        public bool HasSecondaryText => !string.IsNullOrWhiteSpace(SecondaryText);
    }

    public enum ReviewEntryKind
    {
        Spell,
        Miracle,
        Evocation,
        Neuronic,
        Item
    }

    private static class ReviewSectionKeys
    {
        public const string Details = "details";
        public const string Spells = "spells";
        public const string Miracles = "miracles";
        public const string Evocations = "evocations";
        public const string Neuronics = "neuronics";
        public const string Items = "items";
    }
}
