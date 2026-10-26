using System.Collections.ObjectModel;
using System.ComponentModel;
using labyItems.Controls;
using labyItems.Models.ViewModels;
using labyItems.Pages.AbilityCard;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using SpecialisationCardPage = labyItems.Pages.SpecialisationCard.SpecialisationCard;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewView : ContentView
{
    public static readonly BindableProperty ShowSaveButtonProperty = BindableProperty.Create(
        nameof(ShowSaveButton), typeof(bool), typeof(CharacterReviewView), true);

    public static readonly BindableProperty ShowPost8CardProperty = BindableProperty.Create(
        nameof(ShowPost8Card), typeof(bool), typeof(CharacterReviewView), true);

    public static readonly BindableProperty ShowBuilderSummaryProperty = BindableProperty.Create(
        nameof(ShowBuilderSummary), typeof(bool), typeof(CharacterReviewView), true);

    private INotifyPropertyChanged? _boundVm;
    private bool _isNotesExpanded;

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

    public bool ShowBuilderSummary
    {
        get => (bool)GetValue(ShowBuilderSummaryProperty);
        set => SetValue(ShowBuilderSummaryProperty, value);
    }

    public ObservableCollection<CharacterReviewLifeScaleRowVm> LifeScaleRows { get; } = new();
    public ObservableCollection<CharacterReviewAbilityRowVm> PreEighthRows { get; } = new();
    public bool HasNoPreEighthRows => PreEighthRows.Count == 0;

    public string RaceDisplayText => ReadVm(vm => vm.Draft.Race);
    public string ClassDisplayText => ReadVm(vm => vm.Draft.Class);
    public string AlignmentDisplayText => BindingContext is WizardVm vm && vm.Draft.Alignment.HasValue
        ? vm.Draft.Alignment.Value.ToString()
        : "—";
    public string PoliticalGuildText => GetGuildText("political");
    public string ProfessionalGuildText => GetGuildText("professional");
    public string SocialGuildText => GetGuildText("social");
    public double PoliticalGuildOpacity => IsGuildSlotOverridden("political") ? 0.45 : 1;
    public double ProfessionalGuildOpacity => IsGuildSlotOverridden("professional") ? 0.45 : 1;
    public double SocialGuildOpacity => IsGuildSlotOverridden("social") ? 0.45 : 1;

    public CharacterReviewView(object bindingContext)
    {
        InitializeComponent();
        BindingContext = bindingContext;
    }

    public CharacterReviewView() => InitializeComponent();

    protected override void OnBindingContextChanged()
    {
        if (_boundVm != null)
            _boundVm.PropertyChanged -= OnVmPropertyChanged;

        base.OnBindingContextChanged();
        _boundVm = BindingContext as INotifyPropertyChanged;
        if (_boundVm != null)
            _boundVm.PropertyChanged += OnVmPropertyChanged;

        RefreshProjection();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WizardVm.IsAdvancementExpanded))
        {
            Post8ExpandedContent.IsVisible = BindingContext is WizardVm { IsAdvancementExpanded: true };
            return;
        }

        if (e.PropertyName is nameof(WizardVm.ClassLevelAbilityRows)
            or nameof(WizardVm.RaceLevelAbilityRows)
            or nameof(WizardVm.GuildReviewRows)
            or nameof(WizardVm.RaceSummary)
            or nameof(WizardVm.ClassSummary)
            or nameof(WizardVm.AlignmentSummary))
        {
            RefreshProjection();
        }
    }

    private void RefreshProjection()
    {
        if (BindingContext is not WizardVm vm)
        {
            LifeScaleRows.Clear();
            PreEighthRows.Clear();
            RaiseProjectionProperties();
            return;
        }

        var classByLevel = vm.ClassLevelAbilityRows
            .Where(row => row != null && !row.IsTableStage)
            .GroupBy(row => row.Level)
            .ToDictionary(group => group.Key, group => group.First());

        LifeScaleRows.Clear();
        for (var level = 1; level <= 8; level++)
        {
            classByLevel.TryGetValue(level, out var row);
            LifeScaleRows.Add(new CharacterReviewLifeScaleRowVm
            {
                Level = level,
                Body = DisplayOrDash(row?.Body),
                Loc = DisplayOrDash(row?.Loc)
            });
        }

        var projected = new List<(int Level, int SourceOrder, string Name, string Key)>();
        AddAbilityRows(projected, vm.ClassLevelAbilityRows, sourceOrder: 0);
        AddAbilityRows(projected, vm.RaceLevelAbilityRows.Where(row => !row.IsTableStage), sourceOrder: 1);

        var ordered = projected
            .Where(row => row.Level is >= 1 and <= 8)
            .OrderBy(row => row.Level)
            .ThenBy(row => row.SourceOrder)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PreEighthRows.Clear();
        for (var index = 0; index < ordered.Count; index++)
        {
            var row = ordered[index];
            PreEighthRows.Add(new CharacterReviewAbilityRowVm
            {
                Level = row.Level,
                Name = row.Name,
                LookupKey = row.Key,
                RowBackgroundColor = AlternatingRowColor(index)
            });
        }

        Post8ExpandedContent.IsVisible = vm.IsAdvancementExpanded;
        RaiseProjectionProperties();
    }

    private static void AddAbilityRows(
        ICollection<(int Level, int SourceOrder, string Name, string Key)> target,
        IEnumerable<LevelAbilityRowVm> rows,
        int sourceOrder)
    {
        foreach (var row in rows ?? Array.Empty<LevelAbilityRowVm>())
        {
            var names = row.AbilityNames ?? Array.Empty<string>();
            var keys = row.AbilityDetailKeys ?? Array.Empty<string>();
            var count = Math.Max(names.Count, keys.Count);
            for (var index = 0; index < count; index++)
            {
                var name = index < names.Count ? (names[index] ?? string.Empty).Trim() : string.Empty;
                var key = index < keys.Count ? (keys[index] ?? string.Empty).Trim() : string.Empty;
                if (name.Length == 0)
                    name = key;
                if (key.Length == 0)
                    key = name;
                if (name.Length > 0)
                    target.Add((row.Level, sourceOrder, name, key));
            }
        }
    }

    private async void OnPreEighthInfoClicked(object sender, EventArgs e)
    {
        if (sender is not ListItemRowView { ActionCommandParameter: CharacterReviewAbilityRowVm row })
            return;

        var navigation = ResolveNavigation();
        if (navigation == null)
            return;

        var detail = await DetailCardLookupService.ResolveDetailAsync(row.LookupKey, row.Name);
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
            await host.DisplayAlert("Ability Details", $"Could not find a detail card for \"{row.Name}\".", "OK");
    }

    private void OnNotesChevronTapped(object sender, TappedEventArgs e)
    {
        _isNotesExpanded = !_isNotesExpanded;
        NotesContentLabel.IsVisible = _isNotesExpanded;
        NotesChevron.IsExpanded = _isNotesExpanded;
    }

    private async void OnOpenAdvancementClicked(object sender, EventArgs e)
    {
        var navigation = ResolveNavigation();
        if (navigation != null && BindingContext is WizardVm vm)
            await navigation.PushAsync(new AdvanceCharacterPage(vm.Draft));
    }

    private string GetGuildText(string guildType)
    {
        if (BindingContext is not WizardVm vm)
            return "—";

        if (IsGuildSlotOverridden(guildType))
            return BaseRaceName(vm.Draft.Race);

        var names = vm.GuildReviewRows
            .Where(row => string.Equals(row.GuildType, guildType, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.GuildName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return names.Count == 0 ? "—" : string.Join(", ", names);
    }

    private bool IsGuildSlotOverridden(string guildType)
    {
        if (BindingContext is not WizardVm vm)
            return false;

        var rules = GuildSlotRules.FromDraft(vm.Draft);
        return guildType.ToLowerInvariant() switch
        {
            "political" => rules.PoliticalSlots <= 0,
            "professional" => rules.ProfessionalSlots <= 0,
            "social" => rules.SocialSlots <= 0,
            _ => false
        };
    }

    private string ReadVm(Func<WizardVm, string?> selector)
    {
        if (BindingContext is not WizardVm vm)
            return "—";
        return DisplayOrDash(selector(vm));
    }

    private static string BaseRaceName(string? race)
    {
        var value = (race ?? string.Empty).Trim();
        var parenthesis = value.IndexOf('(');
        if (parenthesis > 0)
            value = value[..parenthesis].Trim();
        return value.Length == 0 ? "Overridden" : value;
    }

    private static string DisplayOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static Color AlternatingRowColor(int index)
        => index % 2 == 0 ? Colors.White : Color.FromArgb("#FAF8F3");

    private void RaiseProjectionProperties()
    {
        OnPropertyChanged(nameof(HasNoPreEighthRows));
        OnPropertyChanged(nameof(RaceDisplayText));
        OnPropertyChanged(nameof(ClassDisplayText));
        OnPropertyChanged(nameof(AlignmentDisplayText));
        OnPropertyChanged(nameof(PoliticalGuildText));
        OnPropertyChanged(nameof(ProfessionalGuildText));
        OnPropertyChanged(nameof(SocialGuildText));
        OnPropertyChanged(nameof(PoliticalGuildOpacity));
        OnPropertyChanged(nameof(ProfessionalGuildOpacity));
        OnPropertyChanged(nameof(SocialGuildOpacity));
    }

    private INavigation? ResolveNavigation()
    {
        if (Navigation?.NavigationStack is { Count: > 0 })
            return Navigation;
        return Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
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
}
