using System.Collections.ObjectModel;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;
using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using ClassCardPage = labyItems.Pages.ClassCard.ClassCard;

namespace labyItems.Pages.RaceCard;

public partial class RaceDetailCardView : ContentView
{
    public static readonly BindableProperty RaceNameProperty = BindableProperty.Create(nameof(RaceName), typeof(string), typeof(RaceDetailCardView), string.Empty, propertyChanged: Changed);
    public static readonly BindableProperty RaceRecordProperty = BindableProperty.Create(nameof(RaceRecord), typeof(PeopleRecord), typeof(RaceDetailCardView), null, propertyChanged: Changed);
    private int _rebuildVersion;
    private SubtypeOption? _selectedSubtype;
    private IReadOnlyList<EvolutionService.AbilityResult> _abilities = Array.Empty<EvolutionService.AbilityResult>();
    private IReadOnlyDictionary<string, CharacterClassRecord> _classes = new Dictionary<string, CharacterClassRecord>();
    private IReadOnlyDictionary<string, PeopleRecord> _races = new Dictionary<string, PeopleRecord>();

    public string RaceName { get => (string)GetValue(RaceNameProperty); set => SetValue(RaceNameProperty, value); }
    public PeopleRecord? RaceRecord { get => (PeopleRecord?)GetValue(RaceRecordProperty); set => SetValue(RaceRecordProperty, value); }
    public ObservableCollection<RaceProgressionRow> LevelRows { get; } = new();
    public ObservableCollection<RacePostEighthRow> PostEighthRows { get; } = new();
    public ObservableCollection<RaceAvailableClassRow> AvailableClassRows { get; } = new();
    public ObservableCollection<DetailRow> PropertyRows { get; } = new();
    public ObservableCollection<SubtypeOption> SubtypeOptions { get; } = new();
    public bool HasSubtypeOptions => SubtypeOptions.Count > 0;
    public bool HasNoSubtypeOptions => !HasSubtypeOptions;
    public SubtypeOption? SelectedSubtype
    {
        get => _selectedSubtype;
        set
        {
            if (ReferenceEquals(_selectedSubtype, value)) return;
            _selectedSubtype = value;
            OnPropertyChanged();
            RefreshPostEighthRows();
        }
    }
    public bool HasLevels => LevelRows.Count > 0;
    public bool HasLevelArmour => LevelRows.Any(item => !string.IsNullOrWhiteSpace(item.Row.ArmourText));
    public bool HasPostEighth => PostEighthRows.Count > 0;
    public bool HasAvailableClasses => AvailableClassRows.Count > 0;
    public bool IsAvailableClassesExpanded { get; private set; }
    public bool IsNonStandard => RaceRecord?.NonStandard == true;
    public string PeopleTypeText => PeopleTypeFormatting.FormatPeopleTypes(PeopleTypeFormatting.NormalizePeopleTypes(RaceRecord?.PeopleType));
    public string Icon => PeopleTypeFormatting.IconForPeopleType(PeopleTypeFormatting.SelectPrimaryPeopleType(PeopleTypeFormatting.NormalizePeopleTypes(RaceRecord?.PeopleType)));
    public string BuyAsText => ValueOrDash(RaceRecord?.BuyAs);
    public string AlignmentText => ValueOrDash(NotationHelper.FormatAlignment(RaceRecord?.AlignmentRule));
    public string DescriptionText => RaceRecord?.Description?.Trim() ?? string.Empty;
    public bool HasDescription => DescriptionText.Length > 0;
    public int DescriptionMaxLines => IsDescriptionExpanded ? -1 : 4;
    public bool ShowDescriptionMore => CanExpandDescription && !IsDescriptionExpanded;
    public bool IsDescriptionExpanded { get; private set; }
    public bool CanExpandDescription { get; private set; }

    public RaceDetailCardView() => InitializeComponent();
    private static void Changed(BindableObject bindable, object oldValue, object newValue) => ((RaceDetailCardView)bindable).Rebuild();

    private async void Rebuild()
    {
        var version = ++_rebuildVersion;
        LevelRows.Clear(); PostEighthRows.Clear(); AvailableClassRows.Clear(); PropertyRows.Clear(); SubtypeOptions.Clear();
        IsAvailableClassesExpanded = false;
        IsDescriptionExpanded = false;
        CanExpandDescription = false;
        var record = RaceRecord;
        if (record != null)
        {
            var progressionIndex = 0;
            foreach (var entry in record.LevelledAbilities.OrderBy(entry => StageSort(entry.Key)))
                if (TryStage(entry.Key, out var level, out var table))
                    LevelRows.Add(new RaceProgressionRow(LevelAbilityRowBuilder.Build(level, entry.Value, isTableStage: table), RowColor(progressionIndex++)));

            foreach (var option in ResolveSubtypeOptions(record.Subtype?.OptionsSource))
                SubtypeOptions.Add(new SubtypeOption(DisplaySubtype(option), option));
            _selectedSubtype = SubtypeOptions.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedSubtype));

            Add("ADDITIONAL INFO", record.AdditionalInfo);
            Add("TAGS", string.Join(", ", record.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag))));
            Add("GUILDS", Pages.ClassCard.ClassDetailCardView.FormatGuilds(record.GuildOverrides));

            try
            {
                var abilitiesTask = EvolutionService.GetAllAbilitiesAsync();
                var racesTask = PeopleService.GetAllAsync();
                var classesTask = ClassService.GetAllAsync();
                var classNamesTask = LifeScalesService.GetClassesForRaceAsync(RaceName);
                await Task.WhenAll(abilitiesTask, racesTask, classesTask, classNamesTask);
                if (version != _rebuildVersion) return;
                _abilities = abilitiesTask.Result;
                _races = racesTask.Result;
                _classes = classesTask.Result;
                RefreshPostEighthRows();

                foreach (var className in classNamesTask.Result.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    var points = await LifeScalesService.GetLifeScaleAsync(RaceName, className);
                    if (version != _rebuildVersion) return;
                    var levelEight = points.Count >= 8 ? points[7] : points.LastOrDefault();
                    if (levelEight != null) AvailableClassRows.Add(new RaceAvailableClassRow(className, $"{levelEight.Body} / {levelEight.Loc}", RowColor(AvailableClassRows.Count)));
                }
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("RACE_DETAIL", "Failed loading race detail reference rows.", ex);
            }
        }
        RaiseAll();
    }

    private void OnAvailableClassesTapped(object? sender, TappedEventArgs e) { IsAvailableClassesExpanded = !IsAvailableClassesExpanded; OnPropertyChanged(nameof(IsAvailableClassesExpanded)); }
    private async void OnSubtypeTapped(object? sender, TappedEventArgs e)
    {
        if (SubtypeOptions.Count == 0 || GetParentPage() is not Page page) return;
        var labels = SubtypeOptions.Select(option => option.DisplayName).ToArray();
        var selected = await page.DisplayActionSheet("Race subtype", "Cancel", null, labels);
        if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel") return;
        SelectedSubtype = SubtypeOptions.FirstOrDefault(option => option.DisplayName.Equals(selected, StringComparison.Ordinal));
    }
    private void OnDescriptionTapped(object? sender, TappedEventArgs e)
    {
        if (!CanExpandDescription) return;
        IsDescriptionExpanded = !IsDescriptionExpanded;
        OnPropertyChanged(nameof(IsDescriptionExpanded));
        OnPropertyChanged(nameof(DescriptionMaxLines));
        OnPropertyChanged(nameof(ShowDescriptionMore));
    }
    private void OnDescriptionSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not Label label || label.Width <= 0 || DescriptionText.Length == 0) return;
        var probe = new Label { Text = DescriptionText, FontSize = label.FontSize, FontFamily = label.FontFamily, LineBreakMode = LineBreakMode.WordWrap };
        var fullHeight = probe.Measure(label.Width, double.PositiveInfinity).Height;
        var fourLineHeight = Math.Max(1, label.FontSize * 1.35 * 4);
        var canExpand = fullHeight > fourLineHeight + 1;
        if (CanExpandDescription == canExpand) return;
        CanExpandDescription = canExpand;
        OnPropertyChanged(nameof(CanExpandDescription));
        OnPropertyChanged(nameof(ShowDescriptionMore));
    }
    private async void OnProgressionInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not ListItemRowView { ActionCommandParameter: RaceProgressionRow item } || !item.Row.HasAbilityDetails || GetParentPage() is not Page page) return;
        var row = item.Row;
        var key = row.AbilityDetailKeys.FirstOrDefault() ?? row.AbilityNames.FirstOrDefault();
        var name = row.AbilityNames.FirstOrDefault() ?? key;
        if (string.IsNullOrWhiteSpace(key)) return;
        var detail = await DetailCardLookupService.ResolveDetailAsync(key, name);
        if (detail?.Ability != null) await page.Navigation.PushAsync(new AbilityCardPage(detail.Ability));
    }
    private async void OnPostEighthInfoClicked(object? sender, EventArgs e)
    {
        if (sender is ListItemRowView { ActionCommandParameter: RacePostEighthRow row } && GetParentPage() is Page page)
            await page.Navigation.PushAsync(new AbilityCardPage(row.Ability));
    }
    private async void OnClassInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not ListItemRowView { ActionCommandParameter: RaceAvailableClassRow row }) return;
        var classes = await ClassService.GetAllAsync();
        if (classes.TryGetValue(row.ClassName, out var record) && GetParentPage() is Page page)
            await page.Navigation.PushAsync(new ClassCardPage(row.ClassName, record));
    }
    private Page? GetParentPage() { Element? current = this; while (current != null) { if (current is Page page) return page; current = current.Parent; } return null; }
    private void Add(string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) PropertyRows.Add(new DetailRow(label, value.Trim())); }
    private void RefreshPostEighthRows()
    {
        PostEighthRows.Clear();
        if (_abilities.Count == 0) { OnPropertyChanged(nameof(HasPostEighth)); return; }
        var subtype = SelectedSubtype?.Value ?? string.Empty;
        var draft = new CharacterDraft { Race = RaceName, RaceSubtype = subtype, RaceSubtypeValue = subtype };
        foreach (var ability in RacePostEighthFilter.Filter(_abilities, draft, _classes, _races, new AbilityAvailabilityService()))
            PostEighthRows.Add(new RacePostEighthRow(ability, RowColor(PostEighthRows.Count)));
        OnPropertyChanged(nameof(HasPostEighth));
    }
    private static IEnumerable<string> ResolveSubtypeOptions(string? source)
    {
        var value = (source ?? string.Empty).Trim();
        if (value.StartsWith("Enum:ElfColours", StringComparison.OrdinalIgnoreCase))
            return Enum.GetNames<labyItems.Models.Enums.ElfColours>();
        if (value.StartsWith("Enum:AthfanalColours", StringComparison.OrdinalIgnoreCase))
            return Enum.GetNames<labyItems.Models.Enums.AthfanalColours>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase);
    }
    private static string DisplaySubtype(string value) => value.Equals("Barbarian", StringComparison.OrdinalIgnoreCase) ? "Tribal Human" : value;
    private void RaiseAll() { foreach (var property in new[] { nameof(RaceName), nameof(HasLevels), nameof(HasLevelArmour), nameof(HasPostEighth), nameof(HasAvailableClasses), nameof(HasSubtypeOptions), nameof(HasNoSubtypeOptions), nameof(IsAvailableClassesExpanded), nameof(IsNonStandard), nameof(PeopleTypeText), nameof(Icon), nameof(BuyAsText), nameof(AlignmentText), nameof(DescriptionText), nameof(HasDescription), nameof(DescriptionMaxLines), nameof(ShowDescriptionMore) }) OnPropertyChanged(property); }
    private static Color RowColor(int index) => index % 2 == 0 ? Colors.White : Color.FromArgb("#FAF8F3");
    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    private static int StageSort(string value) => int.TryParse(new string(value.Where(char.IsDigit).ToArray()), out var number) ? number : int.MaxValue;
    private static bool TryStage(string value, out int level, out bool table) { table = value.Contains("table", StringComparison.OrdinalIgnoreCase) || value.StartsWith('T'); return int.TryParse(new string(value.Where(char.IsDigit).ToArray()), out level); }
    public sealed record DetailRow(string Label, string Value);
    public sealed record SubtypeOption(string DisplayName, string Value);
}

public sealed record RaceProgressionRow(LevelAbilityRowVm Row, Color RowBackgroundColor);

public sealed record RacePostEighthRow(EvolutionService.AbilityResult Ability, Color RowBackgroundColor)
{
    public string DisplayName => Ability.DisplayName;
    public string CostText => Ability.Cost.ToString();
    public string TableText => Ability.Table.ToString();
}
public sealed record RaceAvailableClassRow(string ClassName, string LifeScaleText, Color RowBackgroundColor);
