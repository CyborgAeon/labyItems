using System.Collections.ObjectModel;
using System.Text.Json;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.ClassCard;

public partial class ClassDetailCardView : ContentView
{
    public static readonly BindableProperty ClassNameProperty = BindableProperty.Create(nameof(ClassName), typeof(string), typeof(ClassDetailCardView), string.Empty, propertyChanged: Changed);
    public static readonly BindableProperty ClassRecordProperty = BindableProperty.Create(nameof(ClassRecord), typeof(CharacterClassRecord), typeof(ClassDetailCardView), null, propertyChanged: Changed);

    public string ClassName { get => (string)GetValue(ClassNameProperty); set => SetValue(ClassNameProperty, value); }
    public CharacterClassRecord? ClassRecord { get => (CharacterClassRecord?)GetValue(ClassRecordProperty); set => SetValue(ClassRecordProperty, value); }
    public ObservableCollection<DetailRow> PropertyRows { get; } = new();
    public ObservableCollection<LevelAbilityRowVm> LevelRows { get; } = new();

    public bool IsNonStandard => ClassRecord?.NonStandard == true;
    public bool HasLevels => LevelRows.Count > 0;
    public string BracketText => ClassBracketIconHelper.BuildBracketSubheading(ClassRecord?.Brackets ?? new List<string>(), string.Empty);
    public bool HasSplitIcon => (ClassRecord?.Brackets?.Count ?? 0) > 1;
    public string SingleGlyph => ClassBracketIconHelper.GetBracketGlyph(ClassRecord?.Brackets?.FirstOrDefault());
    public string SplitLeftGlyph => ClassBracketIconHelper.GetBracketGlyph(ClassRecord?.Brackets?.ElementAtOrDefault(0));
    public string SplitRightGlyph => ClassBracketIconHelper.GetBracketGlyph(ClassRecord?.Brackets?.ElementAtOrDefault(1));
    public Color SingleBackground => ClassBracketIconHelper.GetBracketColor(ClassRecord?.Brackets?.FirstOrDefault());
    public Color SplitLeftBackground => ClassBracketIconHelper.GetBracketColor(ClassRecord?.Brackets?.ElementAtOrDefault(0));
    public Color SplitRightBackground => ClassBracketIconHelper.GetBracketColor(ClassRecord?.Brackets?.ElementAtOrDefault(1));
    public string MaxAcText => JsonText(ClassRecord?.MaxAC);
    public string PowerPerLevelText => JsonText(ClassRecord?.PowerPerLevel);
    public string CasterLevelText => ClassRecord?.CasterLevel?.ToString() ?? "—";

    public ClassDetailCardView() { InitializeComponent(); }
    private static void Changed(BindableObject bindable, object oldValue, object newValue) => ((ClassDetailCardView)bindable).Rebuild();

    private void Rebuild()
    {
        PropertyRows.Clear(); LevelRows.Clear();
        var record = ClassRecord;
        if (record != null)
        {
            Add("ARMOUR", Join(record.Armour?.Wearable));
            Add("RESTRICTIONS", Join(record.Armour?.Restrictions));
            Add("POWER BASE", Join(record.Powerbase));
            Add("POWER", record.PowerCalculations is { Count: > 0 } ? string.Join("; ", record.PowerCalculations.Select(p => $"{p.PowerBase}: {p.Calculation}")) : null);
            Add("BUY AS", Join(record.BuyAs));
            Add("PATH", record.Path);
            Add("ALIGNMENT", FormatAlignment(record.AlignmentRule));
            Add("GUILDS", FormatGuilds(record.GuildOverrides));
            foreach (var entry in record.Levels.OrderBy(e => StageSort(e.Key)))
                if (TryStage(entry.Key, out var level, out var table)) LevelRows.Add(LevelAbilityRowBuilder.Build(level, entry.Value, isTableStage: table));
        }
        Raise();
    }

    private void Add(string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) PropertyRows.Add(new(label, value)); }
    private void Raise()
    {
        foreach (var name in new[] { nameof(ClassName), nameof(IsNonStandard), nameof(HasLevels), nameof(BracketText), nameof(HasSplitIcon), nameof(SingleGlyph), nameof(SplitLeftGlyph), nameof(SplitRightGlyph), nameof(SingleBackground), nameof(SplitLeftBackground), nameof(SplitRightBackground), nameof(MaxAcText), nameof(PowerPerLevelText), nameof(CasterLevelText) }) OnPropertyChanged(name);
    }
    private static string Join(IEnumerable<string>? values) => string.Join(", ", values?.Where(v => !string.IsNullOrWhiteSpace(v)) ?? []);
    private static string JsonText(JsonElement? value) => value is null || value.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "—" : value.Value.ToString();
    private static int StageSort(string value) => int.TryParse(new string(value.Where(char.IsDigit).ToArray()), out var n) ? n : int.MaxValue;
    private static bool TryStage(string value, out int level, out bool table) { table = value.Contains("table", StringComparison.OrdinalIgnoreCase) || value.StartsWith('T'); return int.TryParse(new string(value.Where(char.IsDigit).ToArray()), out level); }
    internal static string FormatAlignment(AlignmentRule? rule) => rule == null ? "" : rule.AllowedPairs is { Count: > 0 } ? string.Join(", ", rule.AllowedPairs) : string.Join("; ", new[] { rule.Allowed?.Order is { Count: > 0 } ? $"Order: {Join(rule.Allowed.Order.Select(x => x.ToString()))}" : "", rule.Allowed?.Moral is { Count: > 0 } ? $"Moral: {Join(rule.Allowed.Moral.Select(x => x.ToString()))}" : "" }.Where(x => x.Length > 0));
    internal static string FormatGuilds(GuildOverrideRules? rules) { if (rules == null) return ""; var values = new List<string>(); if (rules.IsCityBound) values.Add("City bound"); AddChannel(values, "Social", rules.Social); AddChannel(values, "Professional", rules.Professional); AddChannel(values, "Political", rules.Political); return string.Join("; ", values); }
    private static void AddChannel(List<string> values, string name, GuildOverrideChannel channel) { if (channel.CanJoin == false) values.Add($"No {name.ToLowerInvariant()} guild"); if (!string.IsNullOrWhiteSpace(channel.GuildPeople)) values.Add($"{name}: {channel.GuildPeople}"); if (channel.ReplacedBy.Count > 0) values.Add($"{name}: {Join(channel.ReplacedBy)}"); }
    public sealed record DetailRow(string Label, string Value);
}
