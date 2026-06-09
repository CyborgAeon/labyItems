using System.Linq;
using System.ComponentModel;
using System.Collections.ObjectModel;
using labyItems.Helpers;
using labyItems.Models.ViewModels;
using labyItems.Pages.AbilityCard;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using Microsoft.Maui.Controls;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using SpecialisationCardPage = labyItems.Pages.SpecialisationCard.SpecialisationCard;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewView : ContentView
{
    private enum ReviewEditDestination
    {
        Details,
        Race,
        Class,
        Specialisation
    }

    public static readonly BindableProperty ShowSaveButtonProperty = BindableProperty.Create(
        nameof(ShowSaveButton),
        typeof(bool),
        typeof(CharacterReviewView),
        true);

    public static readonly BindableProperty ShowPost8CardProperty = BindableProperty.Create(
        nameof(ShowPost8Card),
        typeof(bool),
        typeof(CharacterReviewView),
        true);

    public bool ShowSaveButton
    {
        get => (bool)GetValue(ShowSaveButtonProperty);
        set => SetValue(ShowSaveButtonProperty, value);
    }

    public bool ShowPost8Card
    {
        get => (bool)GetValue(ShowPost8CardProperty);
        set => SetValue(ShowPost8CardProperty, value);
    }

    public CharacterReviewView(object bindingContext)
    {
        InitializeComponent();
        AttachLifecycleHandlers();
        BindingContext = bindingContext;
    }

    public CharacterReviewView()
    {
        InitializeComponent();
        AttachLifecycleHandlers();
    }

    private INotifyPropertyChanged? _boundVm;
    private CancellationTokenSource? _post8ExpandCts;
    private bool _isLevelProgressionExpanded = true;
    private bool _isNotesExpanded;

    public ObservableCollection<ReviewLevelColumnVm> LevelColumns { get; } = new();

    public sealed class ReviewLevelColumnVm
    {
        public int Level { get; init; }
        public string LevelText => Level.ToString();
        public string LifeText { get; init; } = "-";

        public string ClassSkillText { get; init; } = string.Empty;
        public bool HasClassSkill => ClassSkillText.Length > 0;
        public IReadOnlyList<string> ClassSkillNames { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> ClassSkillKeys { get; init; } = Array.Empty<string>();

        public string RaceSkillText { get; init; } = string.Empty;
        public bool HasRaceSkill => RaceSkillText.Length > 0;
        public IReadOnlyList<string> RaceSkillNames { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> RaceSkillKeys { get; init; } = Array.Empty<string>();

        public string WeaponSkillText { get; init; } = string.Empty;
        public bool HasWeaponSkill => WeaponSkillText.Length > 0;
        public IReadOnlyList<string> WeaponSkillNames { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> WeaponSkillKeys { get; init; } = Array.Empty<string>();
    }

    protected override void OnBindingContextChanged()
    {
        if (_boundVm != null)
            _boundVm.PropertyChanged -= OnVmPropertyChanged;

        base.OnBindingContextChanged();

        _post8ExpandCts?.Cancel();
        _boundVm = BindingContext as INotifyPropertyChanged;
        if (_boundVm != null)
            _boundVm.PropertyChanged += OnVmPropertyChanged;

        if (BindingContext is WizardVm vm)
        {
            RefreshLevelProgressionColumns(vm);
            ApplyLevelProgressionState();
            ApplyNotesState();

            Post8ExpandedContent.AbortAnimation("post8-expand");
            Post8ExpandedContent.IsVisible = vm.IsAdvancementExpanded;
            Post8ExpandedContent.HeightRequest = -1;
            Post8ExpandedContent.Opacity = 1;
        }
        else
        {
            LevelColumns.Clear();
            Post8ExpandedContent.AbortAnimation("post8-expand");
            Post8ExpandedContent.IsVisible = false;
            Post8ExpandedContent.HeightRequest = -1;
            Post8ExpandedContent.Opacity = 1;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WizardVm.IsAdvancementExpanded))
        {
            UiDispatchHelper.BeginOnMainThread(() =>
                UiDispatchHelper.RunFireAndForget(
                    HandleAdvancementExpandedChangedAsync,
                    "CHARACTER_REVIEW_ADVANCEMENT_EXPAND_ANIMATION"));
        }

        if (e.PropertyName == nameof(WizardVm.ClassLevelAbilityRows)
            || e.PropertyName == nameof(WizardVm.RaceLevelAbilityRows))
        {
            UiDispatchHelper.BeginOnMainThread(() =>
            {
                if (BindingContext is WizardVm vm)
                    RefreshLevelProgressionColumns(vm);
            });
        }
    }

    private void RefreshLevelProgressionColumns(WizardVm vm)
    {
        var classByLevel = vm.ClassLevelAbilityRows
            .Where(row => row != null)
            .ToDictionary(row => row.Level, row => row);

        var raceByLevel = vm.RaceLevelAbilityRows
            .Where(row => row != null && !row.IsTableStage)
            .ToDictionary(row => row.Level, row => row);

        var chosenSpecialisationOptions = BuildChosenSpecialisationOptions(vm);

        LevelColumns.Clear();
        for (var level = 1; level <= 8; level++)
        {
            classByLevel.TryGetValue(level, out var classRow);
            raceByLevel.TryGetValue(level, out var raceRow);

            var lifeText = BuildLifeText(classRow);
            var classSkillText = ResolveClassSkillText(classRow, chosenSpecialisationOptions);
            var raceSkillText = NormalizeCellText(raceRow?.AbilitiesText);
            var weaponSkillText = NormalizeCellText(BuildWeaponSkillText(classRow, raceRow));

            LevelColumns.Add(new ReviewLevelColumnVm
            {
                Level = level,
                LifeText = lifeText,
                ClassSkillText = classSkillText,
                ClassSkillNames = classRow?.AbilityNames ?? Array.Empty<string>(),
                ClassSkillKeys = classRow?.AbilityDetailKeys ?? Array.Empty<string>(),
                RaceSkillText = raceSkillText,
                RaceSkillNames = raceRow?.AbilityNames ?? Array.Empty<string>(),
                RaceSkillKeys = raceRow?.AbilityDetailKeys ?? Array.Empty<string>(),
                WeaponSkillText = weaponSkillText,
                WeaponSkillNames = weaponSkillText.Length == 0
                    ? Array.Empty<string>()
                    : weaponSkillText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                WeaponSkillKeys = weaponSkillText.Length == 0
                    ? Array.Empty<string>()
                    : weaponSkillText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            });
        }

        BuildLevelProgressionTable();
    }

    private void BuildLevelProgressionTable()
    {
        if (LevelProgressionTableHost == null)
            return;

        LevelProgressionTableHost.Children.Clear();
        LevelProgressionTableHost.RowDefinitions.Clear();
        LevelProgressionTableHost.ColumnDefinitions.Clear();

        LevelProgressionTableHost.RowSpacing = 6;
        LevelProgressionTableHost.ColumnSpacing = 10;

        LevelProgressionTableHost.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(122)));
        for (var index = 0; index < 8; index++)
            LevelProgressionTableHost.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(92)));

        for (var row = 0; row < 5; row++)
            LevelProgressionTableHost.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddTableHeaderCell(0, 0, "", false);
        for (var level = 1; level <= 8; level++)
            AddTableHeaderCell(level, 0, level.ToString(), true);

        AddRowLabel(1, "Life");
        AddRowLabel(2, "Class Skills");
        AddRowLabel(3, "Race Skills");
        AddRowLabel(4, "Weapon Skills");

        foreach (var column in LevelColumns)
        {
            AddValueCell(column.Level, 1, column.LifeText, null);
            AddValueCell(column.Level, 2, column.ClassSkillText, () => _ = OpenProgressionDetailsAsync($"Level {column.Level} class skills", column.ClassSkillNames, column.ClassSkillKeys));
            AddValueCell(column.Level, 3, column.RaceSkillText, () => _ = OpenProgressionDetailsAsync($"Level {column.Level} race skills", column.RaceSkillNames, column.RaceSkillKeys));
            AddValueCell(column.Level, 4, column.WeaponSkillText, () => _ = OpenProgressionDetailsAsync($"Level {column.Level} weapon skills", column.WeaponSkillNames, column.WeaponSkillKeys));
        }
    }

    private void AddTableHeaderCell(int column, int row, string text, bool center)
    {
        var border = CreateTableBorder("#00000000", "#F8FAFC", 0, 10);
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = center ? TextAlignment.Center : TextAlignment.Start,
            VerticalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(6, 8, 6, 8),
            TextColor = Color.FromArgb("#111827")
        };
        border.Content = label;
        LevelProgressionTableHost.Add(border, column, row);
    }

    private void AddRowLabel(int row, string text)
    {
        var border = CreateTableBorder("#00000000", "#F8FAFC", 0, 10);
        border.Content = new Label
        {
            Text = text,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(8, 8, 8, 8),
            TextColor = Color.FromArgb("#111827")
        };
        LevelProgressionTableHost.Add(border, 0, row);
    }

    private void AddValueCell(int column, int row, string? text, Action? tapped)
    {
        var value = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
        var border = CreateTableBorder("#00000000", "#FFFFFF", 0, 10);
        var label = new Label
        {
            Text = value,
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = new Thickness(6, 8, 6, 8),
            TextColor = value == "-" ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#111827")
        };

        if (row == 1)
            label.LineBreakMode = LineBreakMode.NoWrap;

        if (tapped != null && value != "-")
        {
            var recognizer = new TapGestureRecognizer();
            recognizer.Tapped += (_, __) => tapped();
            border.GestureRecognizers.Add(recognizer);
        }

        border.Content = label;
        LevelProgressionTableHost.Add(border, column, row);
    }

    private static Border CreateTableBorder(string strokeColor, string backgroundColor, double strokeThickness, float cornerRadius)
        => new()
        {
            Stroke = Color.FromArgb(strokeColor),
            StrokeThickness = strokeThickness,
            BackgroundColor = Color.FromArgb(backgroundColor),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = cornerRadius },
            Padding = 0,
            Margin = new Thickness(0)
        };

    private static string BuildLifeText(LevelAbilityRowVm? row)
    {
        if (row == null)
            return "-";

        var body = (row.Body ?? string.Empty).Trim();
        var loc = (row.Loc ?? string.Empty).Trim();
        if (body.Length == 0 && loc.Length == 0)
            return "-";
        if (body.Length > 0 && loc.Length > 0)
            return $"{body}/{loc}";

        return body.Length > 0 ? body : loc;
    }

    private static string BuildWeaponSkillText(LevelAbilityRowVm? classRow, LevelAbilityRowVm? raceRow)
    {
        var all = new List<string>();
        AddCsv(all, classRow?.WeaponSkills);
        AddCsv(all, raceRow?.WeaponSkills);

        return string.Join(", ", all
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static void AddCsv(List<string> target, string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return;

        foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            target.Add(item);
    }

    private static string NormalizeCellText(string? text)
    {
        var value = (text ?? string.Empty).Trim();
        return value.Length == 0 ? string.Empty : value;
    }

    private static IReadOnlyList<string> BuildChosenSpecialisationOptions(WizardVm vm)
    {
        var selected = new List<string>();
        foreach (var line in vm.SpecialisationSummaryLines)
        {
            var option = (line.SelectedOption ?? string.Empty).Trim();
            if (option.Length == 0)
            {
                var text = (line.Text ?? string.Empty).Trim();
                var separator = text.IndexOf(':');
                if (separator >= 0 && separator < text.Length - 1)
                    option = text[(separator + 1)..].Trim();
            }

            if (option.Length > 0)
                selected.Add(option);
        }

        return selected
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveClassSkillText(LevelAbilityRowVm? classRow, IReadOnlyList<string> chosenSpecialisationOptions)
    {
        var baseText = NormalizeCellText(classRow?.AbilitiesText);
        if (baseText.Length == 0)
            return baseText;

        if (chosenSpecialisationOptions.Count == 0)
            return baseText;

        var joinedSelections = string.Join(", ", chosenSpecialisationOptions);
        if (baseText.Equals("specialisation option", StringComparison.OrdinalIgnoreCase)
            || baseText.Equals("specialization option", StringComparison.OrdinalIgnoreCase)
            || baseText.Equals("specialisation options", StringComparison.OrdinalIgnoreCase)
            || baseText.Equals("specialization options", StringComparison.OrdinalIgnoreCase))
        {
            return joinedSelections;
        }

        var replaced = baseText
            .Replace("Specialisation Option", joinedSelections, StringComparison.OrdinalIgnoreCase)
            .Replace("Specialization Option", joinedSelections, StringComparison.OrdinalIgnoreCase)
            .Replace("Specialisation Options", joinedSelections, StringComparison.OrdinalIgnoreCase)
            .Replace("Specialization Options", joinedSelections, StringComparison.OrdinalIgnoreCase);

        return replaced;
    }

    private void ApplyLevelProgressionState()
    {
        if (LevelProgressionExpandedContent == null || LevelProgressionChevron == null)
            return;

        LevelProgressionExpandedContent.IsVisible = _isLevelProgressionExpanded;
        LevelProgressionChevron.IsExpanded = _isLevelProgressionExpanded;
    }

    private void ApplyNotesState()
    {
        if (NotesContentLabel == null || NotesChevron == null)
            return;

        NotesContentLabel.IsVisible = _isNotesExpanded;
        NotesChevron.IsExpanded = _isNotesExpanded;
    }

    private async void OnLevelProgressionClassCellClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ReviewLevelColumnVm column)
            return;

        await OpenProgressionDetailsAsync(
            $"Level {column.Level} class skills",
            column.ClassSkillNames,
            column.ClassSkillKeys);
    }

    private async void OnLevelProgressionRaceCellClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ReviewLevelColumnVm column)
            return;

        await OpenProgressionDetailsAsync(
            $"Level {column.Level} race skills",
            column.RaceSkillNames,
            column.RaceSkillKeys);
    }

    private async void OnLevelProgressionWeaponCellClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ReviewLevelColumnVm column)
            return;

        await OpenProgressionDetailsAsync(
            $"Level {column.Level} weapon skills",
            column.WeaponSkillNames,
            column.WeaponSkillKeys);
    }

    private async Task OpenProgressionDetailsAsync(string title, IReadOnlyList<string> names, IReadOnlyList<string> keys)
    {
        var options = BuildChoiceOptions(names, keys);
        if (options.Count == 0)
            return;

        var navigation = ResolveNavigation();
        if (navigation == null)
            return;

        if (options.Count == 1)
        {
            await OpenDetailAsync(navigation, options[0].DisplayName, options[0].LookupKey);
            return;
        }

        await navigation.PushAsync(new AbilityCardOptionListPage(title, options));
    }

    private static List<ChoiceSetAbilityRowVm> BuildChoiceOptions(IReadOnlyList<string> names, IReadOnlyList<string> keys)
    {
        var options = new List<ChoiceSetAbilityRowVm>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pairedCount = Math.Min(names.Count, keys.Count);

        for (var i = 0; i < pairedCount; i++)
        {
            var displayName = (names[i] ?? string.Empty).Trim();
            var lookupKey = (keys[i] ?? string.Empty).Trim();
            if (displayName.Length == 0 && lookupKey.Length == 0)
                continue;

            var token = $"{displayName}::{lookupKey}";
            if (!seen.Add(token))
                continue;

            options.Add(new ChoiceSetAbilityRowVm(
                displayName.Length > 0 ? displayName : lookupKey,
                lookupKey.Length > 0 ? lookupKey : displayName));
        }

        foreach (var key in keys.Skip(pairedCount))
        {
            var value = (key ?? string.Empty).Trim();
            if (value.Length == 0 || !seen.Add($"::{value}"))
                continue;

            options.Add(new ChoiceSetAbilityRowVm(value, value));
        }

        foreach (var name in names.Skip(pairedCount))
        {
            var value = (name ?? string.Empty).Trim();
            if (value.Length == 0 || !seen.Add($"{value}::{value}"))
                continue;

            options.Add(new ChoiceSetAbilityRowVm(value, value));
        }

        return options;
    }

    private async Task OpenDetailAsync(INavigation navigation, string displayName, string lookupKey)
    {
        var detail = await DetailCardLookupService.ResolveDetailAsync(lookupKey, displayName);
        if (detail?.Ability != null)
        {
            await navigation.PushAsync(new AbilityCardPage(detail.Ability));
            return;
        }

        if (detail?.Specialisation != null)
        {
            await navigation.PushAsync(new SpecialisationCardPage(detail.Key, detail.Specialisation));
            return;
        }

        var host = ResolveHostPage();
        if (host != null)
            await host.DisplayAlert("Ability Details", $"Could not find a detail card for \"{displayName}\".", "OK");
    }

    private void OnLevelProgressionChevronTapped(object sender, TappedEventArgs e)
    {
        _isLevelProgressionExpanded = !_isLevelProgressionExpanded;
        ApplyLevelProgressionState();
    }

    private void OnNotesChevronTapped(object sender, TappedEventArgs e)
    {
        _isNotesExpanded = !_isNotesExpanded;
        ApplyNotesState();
    }

    private async void OnEditNotesClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Details);
    }

    private async void OnOpenAdvancementClicked(object sender, EventArgs e)
    {
        var nav = ResolveNavigation();
        if (nav == null || BindingContext is not WizardVm vm)
            return;

        await nav.PushAsync(new AdvanceCharacterPage(vm.Draft));
    }

    private async Task HandleAdvancementExpandedChangedAsync()
    {
        if (BindingContext is not WizardVm vm) return;

        var previousCts = _post8ExpandCts;
        previousCts?.Cancel();
        _post8ExpandCts = new CancellationTokenSource();
        previousCts?.Dispose();
        var token = _post8ExpandCts.Token;

        try
        {
            await AnimatePost8ExpandedContentAsync(vm.IsAdvancementExpanded, token);
        }
        catch (TaskCanceledException)
        {
            // Ignore rapid expand/collapse interactions.
        }
        catch (OperationCanceledException)
        {
            // Ignore rapid expand/collapse interactions.
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(
                "CHARACTER_REVIEW_ADVANCEMENT_EXPAND_ANIMATION",
                "Post-8 advancement expand animation failed.",
                ex);
        }
    }

    private async Task AnimatePost8ExpandedContentAsync(bool expand, CancellationToken token)
    {
        if (Post8ExpandedContent == null)
            return;

        Post8ExpandedContent.AbortAnimation("post8-expand");

        if (expand)
        {
            Post8ExpandedContent.IsVisible = true;
            Post8ExpandedContent.Opacity = 0;
            Post8ExpandedContent.HeightRequest = -1;

            await Task.Yield();
            await Task.Delay(1, token);

            var width = CardExpandAnimationHelper.ResolveMeasureWidth(Post8ExpandedContent, Post8CardFrame, this);
            var measured = width > 0
                ? CardExpandAnimationHelper.MeasureContentHeight(Post8ExpandedContent, width)
                : -1;

            if (measured <= 0)
            {
                Post8ExpandedContent.Opacity = 1;
                Post8ExpandedContent.HeightRequest = -1;
                return;
            }

            Post8ExpandedContent.HeightRequest = 0;
            Post8ExpandedContent.Opacity = 0;
            await CardExpandAnimationHelper.AnimateHeightAsync(
                owner: this,
                target: Post8ExpandedContent,
                animationName: "post8-expand",
                from: 0,
                to: measured,
                length: 240,
                easing: Easing.CubicOut,
                onStep: v => Post8ExpandedContent.Opacity = Math.Min(1, v / measured),
                cancellationToken: token);

            if (token.IsCancellationRequested) return;

            Post8ExpandedContent.HeightRequest = -1;
            Post8ExpandedContent.Opacity = 1;
            return;
        }

        if (!Post8ExpandedContent.IsVisible)
            return;

        var startHeight = Post8ExpandedContent.Height;
        if (startHeight <= 0)
        {
            Post8ExpandedContent.IsVisible = false;
            Post8ExpandedContent.HeightRequest = -1;
            Post8ExpandedContent.Opacity = 1;
            return;
        }

        Post8ExpandedContent.HeightRequest = startHeight;
        await CardExpandAnimationHelper.AnimateHeightAsync(
            owner: this,
            target: Post8ExpandedContent,
            animationName: "post8-expand",
            from: startHeight,
            to: 0,
            length: 200,
            easing: Easing.CubicIn,
            onStep: v => Post8ExpandedContent.Opacity = startHeight <= 0 ? 0 : Math.Max(0, v / startHeight),
            cancellationToken: token);

        if (token.IsCancellationRequested) return;

        Post8ExpandedContent.IsVisible = false;
        Post8ExpandedContent.HeightRequest = -1;
        Post8ExpandedContent.Opacity = 1;
    }

    private async void OnViewSpecialisationAbilityDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.CommandParameter is not WizardVm.SpecialisationSummaryLineVm line || !line.HasDetails)
            return;

        var nav = ResolveNavigation();
        if (nav == null)
            return;

        var keysToTry = new List<string>();
        var directKey = (line.SpecialisationKey ?? string.Empty).Trim();
        if (directKey.Length > 0)
            keysToTry.Add(directKey);

        var parsedFromSummary = ExtractSpecialisationTitleFromSummary(line.Text);
        if (parsedFromSummary.Length > 0
            && !keysToTry.Contains(parsedFromSummary, StringComparer.OrdinalIgnoreCase))
        {
            keysToTry.Add(parsedFromSummary);
        }

        foreach (var key in keysToTry)
        {
            var match = await DetailCardLookupService.FindSpecialisationAsync(key);
            if (!string.IsNullOrWhiteSpace(match.Key) && match.Record != null)
            {
                await nav.PushAsync(new SpecialisationCardPage(match.Key, match.Record, line.SelectedOption));
                return;
            }
        }

        if (line.Ability != null)
            await nav.PushAsync(new AbilityCardPage(line.Ability));
    }

    private async void OnEditIdentityClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Details);
    }

    private async void OnEditDetailsClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Details);
    }

    private async void OnEditRaceClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Race);
    }

    private async void OnEditClassClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Class);
    }

    private async void OnEditSpecialisationClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Specialisation);
    }

    private async void OnEditGuildChoicesClicked(object sender, EventArgs e)
    {
        if (BindingContext is not WizardVm vm)
            return;

        if (sender is not Button button || button.CommandParameter is not GuildReviewSummaryRowVm row)
            return;

        if (!row.HasChoices)
            return;

        var hostPage = ResolveHostPage();
        if (hostPage == null)
            return;

        await GuildBenefitChoicePromptHelper.EditChoicesForGuildAsync(
            hostPage,
            vm.GuildsVm,
            row.GuildName,
            refreshAfterSelection: () => vm.RefreshReviewAsync());
    }

    private async void OnViewGuildDetailsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not WizardVm vm)
            return;

        if (sender is not Button button || button.CommandParameter is not GuildReviewSummaryRowVm row)
            return;

        var detailCard = await vm.GuildsVm.BuildGuildDetailCardAsync(row.GuildName, expand: true);
        if (detailCard == null)
            return;

        var navigation = ResolveNavigation();
        if (navigation == null)
            return;

        var detailView = new GuildCardView
        {
            BindingContext = detailCard,
            ToggleExpandedCommand = new Command<GuildCardVm>(card =>
            {
                if (card == null)
                    return;
                card.IsExpanded = !card.IsExpanded;
            }),
            SelectCommand = new Command<GuildCardVm>(_ => { })
        };

        var page = new ContentPage
        {
            Title = detailCard.Name,
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(16, 16, 16, 24),
                    Children = { detailView }
                }
            }
        };

        await navigation.PushAsync(page);
    }

    private async void OnGetBattleboardClicked(object sender, EventArgs e)
    {
        if (BindingContext is not WizardVm vm)
            return;

        var page = ResolveHostPage();
        if (page == null)
            return;

        var choicesComplete = await GuildBenefitChoicePromptHelper.EnsureChoicesCompletedAsync(
            page,
            vm.GuildsVm,
            refreshAfterSelection: () => vm.RefreshReviewAsync(),
            actionLabel: "getting battleboard output");
        if (!choicesComplete)
            return;

        const string cancel = "cancel";
        const string emailToDesk = "email to desk";
        const string downloadPdf = "download as pdf";
        const string downloadExcel = "download as excel";

        var selected = await page.DisplayActionSheet(
            "Get battleboard",
            cancel,
            null,
            emailToDesk,
            downloadPdf,
            downloadExcel);

        if (string.IsNullOrWhiteSpace(selected) || selected.Equals(cancel, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (selected.Equals(emailToDesk, StringComparison.OrdinalIgnoreCase))
            {
                await vm.EmailBattleboardPdfToDeskAsync();
                return;
            }

            if (selected.Equals(downloadPdf, StringComparison.OrdinalIgnoreCase))
            {
                await vm.DownloadBattleboardAsPdfAsync();
                return;
            }

            if (selected.Equals(downloadExcel, StringComparison.OrdinalIgnoreCase))
            {
                await vm.DownloadBattleboardAsExcelAsync();
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("BATTLEBOARD_EXPORT", "Battleboard export action failed.", ex);
            await page.DisplayAlert(
                "Battleboard export failed",
                BuildDetailedExceptionMessage(ex),
                "OK");
        }
    }

    private async Task NavigateToEditDestinationAsync(ReviewEditDestination destination)
    {
        if (BindingContext is not WizardVm vm)
            return;

        var hostPage = ResolveHostPage();
        if (hostPage is Wizard)
        {
            await vm.NavigateToEditTargetAsync(MapDestination(destination));
            return;
        }

        var nav = ResolveNavigation();
        if (nav == null)
            return;

        var wizardPage = new Wizard(vm.Draft, async () => await nav.PopAsync());
        await nav.PushAsync(wizardPage);

        if (wizardPage.BindingContext is WizardVm wizardVm)
            await wizardVm.NavigateToEditTargetAsync(MapDestination(destination));
    }

    private static WizardEditTarget MapDestination(ReviewEditDestination destination)
    {
        switch (destination)
        {
            case ReviewEditDestination.Details:
                return WizardEditTarget.Details;
            case ReviewEditDestination.Race:
                return WizardEditTarget.Race;
            case ReviewEditDestination.Class:
                return WizardEditTarget.Class;
            case ReviewEditDestination.Specialisation:
                return WizardEditTarget.Specialisation;
            default:
                return WizardEditTarget.Details;
        }
    }

    private static string ExtractSpecialisationTitleFromSummary(string? summaryText)
    {
        var text = (summaryText ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        var colonIndex = text.IndexOf(':');
        if (colonIndex > 0)
            text = text[..colonIndex].Trim();

        var levelSuffixIndex = text.IndexOf(" (Lvl", StringComparison.OrdinalIgnoreCase);
        if (levelSuffixIndex > 0)
            text = text[..levelSuffixIndex].Trim();

        return text;
    }

    private INavigation? ResolveNavigation()
    {
        if (Navigation?.NavigationStack is { Count: > 0 })
            return Navigation;

        if (Shell.Current?.Navigation is { } shellNav)
            return shellNav;

        return Application.Current?.MainPage?.Navigation;
    }

    private Page? ResolveHostPage()
    {
        Element? current = this;
        while (current != null)
        {
            if (current is Page page)
                return page;

            current = current.Parent;
        }

        return Application.Current?.MainPage;
    }

    private static string BuildDetailedExceptionMessage(Exception ex)
    {
        var parts = new List<string>();
        var current = ex;
        while (current != null)
        {
            var message = (current.Message ?? string.Empty).Trim();
            if (message.Length > 0)
                parts.Add(message);

            current = current.InnerException;
        }

        if (parts.Count == 0)
            return "An unknown error occurred. Check runtime.log for details.";

        var combined = string.Join("\n\n", parts.Distinct(StringComparer.Ordinal));
        return $"{combined}\n\nLog: {RuntimeLog.LogPath}";
    }

    private void AttachLifecycleHandlers()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_boundVm != null)
            return;

        _boundVm = BindingContext as INotifyPropertyChanged;
        if (_boundVm != null)
            _boundVm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_boundVm != null)
            _boundVm.PropertyChanged -= OnVmPropertyChanged;

        _boundVm = null;
        _post8ExpandCts?.Cancel();
        _post8ExpandCts?.Dispose();
        _post8ExpandCts = null;

        Post8ExpandedContent?.AbortAnimation("post8-expand");
    }
}
