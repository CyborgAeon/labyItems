using System.Collections.ObjectModel;
using labyItems.Helpers;
using labyItems.Models.ViewModels;
using labyItems.Services;

namespace labyItems.Pages.RaceCard;

public partial class RaceDetailCardView : ContentView
{
    public static readonly BindableProperty RaceNameProperty = BindableProperty.Create(nameof(RaceName), typeof(string), typeof(RaceDetailCardView), string.Empty, propertyChanged: Changed);
    public static readonly BindableProperty RaceRecordProperty = BindableProperty.Create(nameof(RaceRecord), typeof(PeopleRecord), typeof(RaceDetailCardView), null, propertyChanged: Changed);
    public string RaceName { get => (string)GetValue(RaceNameProperty); set => SetValue(RaceNameProperty, value); }
    public PeopleRecord? RaceRecord { get => (PeopleRecord?)GetValue(RaceRecordProperty); set => SetValue(RaceRecordProperty, value); }
    public ObservableCollection<LevelAbilityRowVm> LevelRows { get; } = new();
    public ObservableCollection<DetailRow> PropertyRows { get; } = new();
    public IReadOnlyList<string> Tags => RaceRecord?.Tags?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
    public bool HasTags => Tags.Count > 0;
    public bool HasLevels => LevelRows.Count > 0;
    public bool IsNonStandard => RaceRecord?.NonStandard == true;
    public string PeopleTypeText => PeopleTypeFormatting.FormatPeopleTypes(PeopleTypeFormatting.NormalizePeopleTypes(RaceRecord?.PeopleType));
    public string Icon => PeopleTypeFormatting.IconForPeopleType(PeopleTypeFormatting.SelectPrimaryPeopleType(PeopleTypeFormatting.NormalizePeopleTypes(RaceRecord?.PeopleType)));
    public string DescriptionText => RaceRecord?.Description?.Trim() ?? string.Empty;
    public bool HasDescription => DescriptionText.Length > 0;

    public RaceDetailCardView() { InitializeComponent(); }
    private static void Changed(BindableObject bindable, object oldValue, object newValue) => ((RaceDetailCardView)bindable).Rebuild();
    private void Rebuild()
    {
        PropertyRows.Clear(); LevelRows.Clear();
        var record = RaceRecord;
        if (record != null)
        {
            Add("ADDITIONAL INFO", record.AdditionalInfo);
            if (record.Subtype != null)
            {
                var subtype = string.Join(" · ", new[] { record.Subtype.DisplayName, record.Subtype.Description, record.Subtype.SelectionMode, record.Subtype.OptionsSource }.Where(x => !string.IsNullOrWhiteSpace(x)));
                Add("SUBTYPE", subtype);
            }
            Add("BUY AS", record.BuyAs);
            Add("ALIGNMENT", NotationHelper.FormatAlignment(record.AlignmentRule));
            Add("GUILDS", ClassCard.ClassDetailCardView.FormatGuilds(record.GuildOverrides));
            foreach (var entry in record.LevelledAbilities.OrderBy(e => StageSort(e.Key)))
                if (TryStage(entry.Key, out var level, out var table)) LevelRows.Add(LevelAbilityRowBuilder.Build(level, entry.Value, isTableStage: table));
        }
        foreach (var name in new[] { nameof(RaceName), nameof(Tags), nameof(HasTags), nameof(HasLevels), nameof(IsNonStandard), nameof(PeopleTypeText), nameof(Icon), nameof(DescriptionText), nameof(HasDescription) }) OnPropertyChanged(name);
    }
    private void Add(string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) PropertyRows.Add(new(label, value.Trim())); }
    private static int StageSort(string value) => int.TryParse(new string(value.Where(char.IsDigit).ToArray()), out var n) ? n : int.MaxValue;
    private static bool TryStage(string value, out int level, out bool table) { table = value.Contains("table", StringComparison.OrdinalIgnoreCase) || value.StartsWith('T'); return int.TryParse(new string(value.Where(char.IsDigit).ToArray()), out level); }
    public sealed record DetailRow(string Label, string Value);
}
