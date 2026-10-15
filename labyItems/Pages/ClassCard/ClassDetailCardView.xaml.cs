using System.Collections.ObjectModel;
using System.Text.Json;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Services;
using Microsoft.Maui.Graphics;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using SpecialisationCardPage = labyItems.Pages.SpecialisationCard.SpecialisationCard;

namespace labyItems.Pages.ClassCard;

public partial class ClassDetailCardView : ContentView
{
    public static readonly BindableProperty ClassNameProperty = BindableProperty.Create(nameof(ClassName), typeof(string), typeof(ClassDetailCardView), string.Empty, propertyChanged: Changed);
    public static readonly BindableProperty ClassRecordProperty = BindableProperty.Create(nameof(ClassRecord), typeof(CharacterClassRecord), typeof(ClassDetailCardView), null, propertyChanged: Changed);
    public string ClassName { get => (string)GetValue(ClassNameProperty); set => SetValue(ClassNameProperty, value); }
    public CharacterClassRecord? ClassRecord { get => (CharacterClassRecord?)GetValue(ClassRecordProperty); set => SetValue(ClassRecordProperty, value); }
    public ObservableCollection<DetailRow> FactCells { get; } = new();
    public ObservableCollection<DetailFactRow> FactRows { get; } = new();
    public ObservableCollection<DetailRow> NarrativeRows { get; } = new();
    public ObservableCollection<ClassProgressionRow> ProgressionRows { get; } = new();
    public ObservableCollection<ClassProgressionRow> WeaponSkillRows { get; } = new();
    public ObservableCollection<ClassWeaponSkillLevelRow> WeaponSkillLevelRows { get; } = new();
    private int _rebuildVersion;
    public string LifeScaleValue { get; private set; } = "—";
    public string LifeScaleSubtitle { get; private set; } = "8th Lifescale";
    public string MaxAcText => ClassRecord == null ? "—" : JsonText(ClassRecord.MaxAC);
    public string AlignmentText => ValueOrDash(NotationHelper.FormatAlignment(ClassRecord?.AlignmentRule));
    public string ArmourTypesText => ResolveArmourTypes(ClassRecord?.Armour?.Wearable);
    public string BuyAsText => ValueOrDash(Join(ClassRecord?.BuyAs));
    public bool IsProgressionExpanded { get; set; } = true;
    public bool IsWeaponSkillsExpanded { get; set; }
    public bool IsNonStandard => ClassRecord?.NonStandard == true;
    public bool HasProgression => ProgressionRows.Count > 0;
    public bool HasWeaponSkills => WeaponSkillRows.Count > 0;
    public string WeaponSkillsSummary => ClassProgressionProjector.BuildWeaponSkillSummary(WeaponSkillRows);
    public string BracketText => ClassBracketIconHelper.BuildBracketSubheading(ClassRecord?.Brackets ?? [], "Unbracketed");
    public bool HasSplitIcon => (ClassRecord?.Brackets?.Count ?? 0) > 1;
    public string SingleGlyph => ClassBracketIconHelper.GetBracketGlyph(ClassRecord?.Brackets?.FirstOrDefault());
    public string SplitLeftGlyph => ClassBracketIconHelper.GetBracketGlyph(ClassRecord?.Brackets?.ElementAtOrDefault(0));
    public string SplitRightGlyph => ClassBracketIconHelper.GetBracketGlyph(ClassRecord?.Brackets?.ElementAtOrDefault(1));
    public Color SingleBackground => ClassBracketIconHelper.GetBracketColor(ClassRecord?.Brackets?.FirstOrDefault());
    public Color SplitLeftBackground => ClassBracketIconHelper.GetBracketColor(ClassRecord?.Brackets?.ElementAtOrDefault(0));
    public Color SplitRightBackground => ClassBracketIconHelper.GetBracketColor(ClassRecord?.Brackets?.ElementAtOrDefault(1));

    public ClassDetailCardView() => InitializeComponent();
    private static void Changed(BindableObject bindable, object oldValue, object newValue) => ((ClassDetailCardView)bindable).Rebuild();
    private async void Rebuild()
    {
        var rebuildVersion = ++_rebuildVersion;
        FactCells.Clear(); FactRows.Clear(); NarrativeRows.Clear(); ProgressionRows.Clear(); WeaponSkillRows.Clear(); WeaponSkillLevelRows.Clear();
        IsProgressionExpanded = true; IsWeaponSkillsExpanded = false;
        var record = ClassRecord;
        LifeScaleValue = "—";
        LifeScaleSubtitle = "8th Lifescale";
        if (record != null)
        {
            foreach (var power in record.PowerCalculations ?? []) AddFact(string.IsNullOrWhiteSpace(power.PowerBase) ? "POWER" : power.PowerBase.ToUpperInvariant(), ClassPowerCalculationFormatter.Format(power.Calculation));
            foreach (var row in ClassProgressionProjector.BuildWeaponSkillRows(record.Levels))
                WeaponSkillRows.Add(row);
            foreach (var row in ClassProgressionProjector.BuildProgressionRows(record.Levels))
                ProgressionRows.Add(row);
            foreach (var group in WeaponSkillRows.GroupBy(row => row.Level).OrderBy(group => group.Key))
                WeaponSkillLevelRows.Add(new ClassWeaponSkillLevelRow(group.Key, string.Join(" & ", group.Select(row => row.Name))));
            AddNarrative("DESCRIPTION", record.Description); AddNarrative("PATH", record.Path); AddNarrative("TAGS", Join(record.Tags)); AddNarrative("ARMOUR RESTRICTIONS", Join(record.Armour?.Restrictions)); AddNarrative("POWER BASE", Join(record.Powerbase)); AddNarrative("GUILDS", FormatGuilds(record.GuildOverrides));

            try
            {
                var lifeScale = await LifeScalesService.GetDefaultForClassAsync(ClassName);
                if (rebuildVersion != _rebuildVersion) return;
                if (lifeScale != null)
                {
                    LifeScaleValue = $"{lifeScale.LevelEight.Body} / {lifeScale.LevelEight.Loc}";
                    LifeScaleSubtitle = $" 8th level {lifeScale.RaceName}";
                }
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("CLASS_DETAIL_LIFESCALE", "Failed to resolve the default class lifescale.", ex);
            }
        }
        for (var index = 0; index < FactCells.Count; index += 2)
            FactRows.Add(new DetailFactRow(FactCells[index], index + 1 < FactCells.Count ? FactCells[index + 1] : null, index > 0));
        Raise();
    }
    private void AddFact(string label, string value) => FactCells.Add(new(label, string.IsNullOrWhiteSpace(value) ? "—" : value));
    private void AddNarrative(string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) NarrativeRows.Add(new(label, value)); }
    private void Raise() { foreach (var name in new[] { nameof(ClassName), nameof(LifeScaleValue), nameof(LifeScaleSubtitle), nameof(MaxAcText), nameof(AlignmentText), nameof(ArmourTypesText), nameof(BuyAsText), nameof(IsNonStandard), nameof(HasProgression), nameof(HasWeaponSkills), nameof(WeaponSkillsSummary), nameof(IsProgressionExpanded), nameof(IsWeaponSkillsExpanded), nameof(BracketText), nameof(HasSplitIcon), nameof(SingleGlyph), nameof(SplitLeftGlyph), nameof(SplitRightGlyph), nameof(SingleBackground), nameof(SplitLeftBackground), nameof(SplitRightBackground) }) OnPropertyChanged(name); }
    private void OnProgressionTapped(object? sender, TappedEventArgs e) { IsProgressionExpanded = !IsProgressionExpanded; OnPropertyChanged(nameof(IsProgressionExpanded)); }
    private void OnWeaponSkillsTapped(object? sender, TappedEventArgs e) { IsWeaponSkillsExpanded = !IsWeaponSkillsExpanded; OnPropertyChanged(nameof(IsWeaponSkillsExpanded)); }
    private async void OnLifescalesClicked(object? sender, EventArgs e) { if (GetParentPage() is Page page) await page.DisplayAlert("Lifescales", "todo", "OK"); }
    private async void OnAbilityInfoClicked(object? sender, EventArgs e)
    {
        var row = sender is ListItemRowView view ? view.ActionCommandParameter as ClassProgressionRow : null;
        if (row == null) return;
        var lookupKey = row.Definition.AbilityRef ?? row.Definition.Key ?? row.Definition.OverwriteKey ?? row.Name;
        if (!string.IsNullOrWhiteSpace(row.Definition.OverwriteKey))
        {
            var specialisation = await DetailCardLookupService.FindSpecialisationAsync(row.Definition.OverwriteKey);
            if (specialisation.Record != null && GetParentPage() is Page specialisationHost)
            {
                await specialisationHost.Navigation.PushAsync(new SpecialisationCardPage(specialisation.Key, specialisation.Record));
                return;
            }
        }
        var detail = await DetailCardLookupService.ResolveDetailAsync(lookupKey, row.Definition.OverwriteKey, row.Name);
        if (GetParentPage() is not Page page) return;
        if (detail?.Specialisation != null)
        {
            await page.Navigation.PushAsync(new SpecialisationCardPage(detail.Key, detail.Specialisation));
            return;
        }
        var ability = detail?.Ability ?? DetailCardLookupService.ToAbilityResult(row.Definition, lookupKey, row.Name);
        await page.Navigation.PushAsync(new AbilityCardPage(ability));
    }
    private Page? GetParentPage() { Element? current = this; while (current != null) { if (current is Page page) return page; current = current.Parent; } return null; }
    private static string Join(IEnumerable<string>? values) => string.Join(", ", values?.Where(v => !string.IsNullOrWhiteSpace(v)) ?? []);
    private static string ValueOrDash(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "—";
    private static string JsonText(JsonElement value) => value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "—" : value.ToString();
    internal static string FormatGuilds(GuildOverrideRules? rules) { if (rules == null) return ""; var values = new List<string>(); if (rules.IsCityBound) values.Add("City bound"); AddChannel(values, "Social", rules.Social); AddChannel(values, "Professional", rules.Professional); AddChannel(values, "Political", rules.Political); return string.Join("; ", values); }
    private static void AddChannel(List<string> values, string name, GuildOverrideChannel channel) { if (channel.CanJoin == false) values.Add($"No {name.ToLowerInvariant()} guild"); if (!string.IsNullOrWhiteSpace(channel.GuildPeople)) values.Add($"{name}: {channel.GuildPeople}"); if (channel.ReplacedBy.Count > 0) values.Add($"{name}: {Join(channel.ReplacedBy)}"); }
    public sealed record DetailRow(string Label, string Value);
    public sealed record DetailFactRow(DetailRow Left, DetailRow? Right, bool ShowTopDivider)
    {
        public bool HasRight => Right != null;
        public bool HasSingle => Right == null;
    }

    public string ResolveArmourTypes(IEnumerable<string>? armourTypes) => 
    armourTypes.Count() > 2 ? "Any" : ValueOrDash(Join(armourTypes));
}
