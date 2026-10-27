using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.MultiRaceCard;

public partial class MultiRaceDetailCardView : ContentView
{
    public static readonly BindableProperty DefinitionProperty = BindableProperty.Create(nameof(Definition), typeof(MultiRaceDefinition), typeof(MultiRaceDetailCardView), propertyChanged: (b, _, _) => ((MultiRaceDetailCardView)b).Rebuild());
    public MultiRaceDefinition? Definition { get => (MultiRaceDefinition?)GetValue(DefinitionProperty); set => SetValue(DefinitionProperty, value); }
    public ObservableCollection<OptionRow> Options { get; } = new();
    public ObservableCollection<LevelRow> LevelRows { get; } = new();
    public ObservableCollection<AvailabilityRow> AvailabilityRows { get; } = new();
    public ObservableCollection<CostCell> CostCells { get; } = new();
    private MultiRaceAvailabilityOption? _selectedAvailability;
    private string? _selectedOption;

    public string DisplayName => Definition?.DisplayName ?? string.Empty;
    public string IconGlyph => Definition?.IconGlyph ?? string.Empty;
    public bool HasOptions => Options.Count > 0;
    public bool HasAvailability => AvailabilityRows.Count > 0;
    public bool HasLevels => LevelRows.Count > 0;
    public string NotesText => string.Join("\n\n", Definition?.Notes?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? []);
    public bool HasNotes => NotesText.Length > 0;
    public bool IsBracketPure => !string.IsNullOrWhiteSpace(Definition?.RequiresBracketPure);
    public bool RequiresPath => Definition?.AvailabilityOptions?.Any(x => ExtractNotesText(x.Notes).Contains("Path", StringComparison.OrdinalIgnoreCase)) == true;
    public string LevelCapText => (Definition?.MaxLevel ?? 0).ToString();
    public string PreReqsText => Join(_selectedAvailability?.PreReqs) is { Length: > 0 } value ? value : "None";
    public string PowerText => Definition == null ? string.Empty : Join(Definition.SystemEffectsByLevel.Values.SelectMany(x => x ?? []).Where(x => x.EffectType.Contains("Power", StringComparison.OrdinalIgnoreCase)).Select(x => x.Display).Distinct(StringComparer.OrdinalIgnoreCase));
    public bool HasPower => !string.IsNullOrWhiteSpace(PowerText);
    public bool HasNoPower => !HasPower;
    public string SelectedAvailabilityText => _selectedAvailability?.Display ?? "No availability data";

    public MultiRaceDetailCardView() { InitializeComponent(); BindingContext = this; }

    private void Rebuild()
    {
        Options.Clear(); LevelRows.Clear(); AvailabilityRows.Clear(); CostCells.Clear();
        var definition = Definition;
        if (definition != null)
        {
            foreach (var name in definition.Levels.Values.SelectMany(x => x ?? []).SelectMany(x => x.PreReqs ?? []).Select(OptionFromPrerequisite).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)) Options.Add(new(name));
            _selectedAvailability = (definition.AvailabilityOptions ?? []).OrderBy(TotalCost).ThenBy(x => x.Display, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            BuildAvailabilityRows(); BuildCostCells(); BuildLevels();
        }
        RaiseAll();
    }

    private void BuildAvailabilityRows()
    {
        AvailabilityRows.Clear();
        foreach (var option in Definition?.AvailabilityOptions ?? [])
        {
            var selected = ReferenceEquals(option, _selectedAvailability);
            AvailabilityRows.Add(new(option, $"{TotalCost(option)} pts", selected ? Color.FromArgb("#F4E7C4") : RowColor(AvailabilityRows.Count), selected ? Color.FromArgb("#8F2634") : Colors.Transparent, selected ? 1d : 0d));
        }
    }

    private void BuildCostCells()
    {
        CostCells.Clear();
        for (var level = 1; level <= Math.Clamp(Definition?.MaxLevel ?? 0, 0, 8); level++)
            CostCells.Add(new(level, _selectedAvailability?.CostsByLevel.GetValueOrDefault(level.ToString()).ToString() ?? "—", level > 1));
    }

    private void BuildLevels()
    {
        LevelRows.Clear(); if (Definition == null) return;
        foreach (var level in Definition.Levels.Keys.Concat(Definition.SystemEffectsByLevel.Keys).Select(ParseLevel).Where(x => x > 0).Distinct().OrderBy(x => x))
        {
            foreach (var ability in (Definition.Levels.GetValueOrDefault(level.ToString()) ?? []).Where(IsVisibleForOption)) LevelRows.Add(new(level, ability.Name, ability.Effect, ability.AbilityRef, RowColor(LevelRows.Count)));
            foreach (var effect in Definition.SystemEffectsByLevel.GetValueOrDefault(level.ToString()) ?? []) if (!string.IsNullOrWhiteSpace(effect.Display)) LevelRows.Add(new(level, effect.Display, effect.Display, string.Empty, RowColor(LevelRows.Count)));
        }
    }

    private void SelectAvailability(MultiRaceAvailabilityOption option) { _selectedAvailability = option; BuildAvailabilityRows(); BuildCostCells(); RaiseAll(); }
    private void OnAvailabilityRowClicked(object? sender, EventArgs e) { if (sender is ListItemRowView { ActionCommandParameter: AvailabilityRow row }) SelectAvailability(row.Option); }
    private async void OnCostTapped(object? sender, EventArgs e)
    {
        var options = Definition?.AvailabilityOptions ?? [];
        if (options.Count <= 1 || GetParentPage() is not Page page) return;
        var labels = options.Select(x => $"{x.Display} ({TotalCost(x)} pts total)").ToArray();
        var chosen = await page.DisplayActionSheet("Select availability", "Cancel", null, labels);
        var index = Array.IndexOf(labels, chosen); if (index >= 0) SelectAvailability(options[index]);
    }
    private bool IsVisibleForOption(MultiRaceLevelAbility ability) { var choices = (ability.PreReqs ?? []).Select(OptionFromPrerequisite).Where(x => x.Length > 0).ToList(); return choices.Count == 0 || _selectedOption == null || choices.Contains(_selectedOption, StringComparer.OrdinalIgnoreCase); }
    private void OnOptionTapped(object? sender, TappedEventArgs e) { if (e.Parameter is not OptionRow row) return; _selectedOption = string.Equals(_selectedOption, row.Name, StringComparison.OrdinalIgnoreCase) ? null : row.Name; foreach (var option in Options) option.IsSelected = string.Equals(option.Name, _selectedOption, StringComparison.OrdinalIgnoreCase); BuildLevels(); RaiseAll(); }
    private async void OnLevelInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not ListItemRowView { ActionCommandParameter: LevelRow row } || GetParentPage() is not Page page) return;
        var ability = !string.IsNullOrWhiteSpace(row.AbilityRef) ? await DetailCardLookupService.ResolveAbilityAsync(row.AbilityRef) : null;
        ability ??= new EvolutionService.AbilityResult { Index = row.Name, DisplayName = row.Name, Description = row.Detail, AbilityRef = row.AbilityRef, Available = "Multi-race" };
        await page.Navigation.PushAsync(new Pages.AbilityCard.AbilityCard(ability));
    }

    private void RaiseAll() { foreach (var name in new[] { nameof(DisplayName), nameof(IconGlyph), nameof(HasOptions), nameof(HasAvailability), nameof(HasLevels), nameof(NotesText), nameof(HasNotes), nameof(IsBracketPure), nameof(RequiresPath), nameof(LevelCapText), nameof(PreReqsText), nameof(PowerText), nameof(HasPower), nameof(HasNoPower), nameof(SelectedAvailabilityText) }) OnPropertyChanged(name); }
    private Page? GetParentPage() { Element? current = this; while (current != null) { if (current is Page page) return page; current = current.Parent; } return null; }
    private static int TotalCost(MultiRaceAvailabilityOption option) => option.CostsByLevel?.Values.Sum() ?? 0;
    private static string Join(IEnumerable<string>? values) => string.Join(", ", values?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? []);
    private static string ExtractNotesText(JsonElement value) => value.ValueKind switch { JsonValueKind.String => value.GetString() ?? string.Empty, JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(x => x.ToString())), _ => string.Empty };
    private static string OptionFromPrerequisite(string value) { const string marker = ".type."; var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase); return index < 0 ? string.Empty : string.Join(' ', value[(index + marker.Length)..].Split('-', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..])); }
    private static int ParseLevel(string value) => int.TryParse(value, out var result) ? result : 0;
    private static Color RowColor(int index) => index % 2 == 0 ? Colors.White : Color.FromArgb("#FAF8F3");
    public sealed record CostCell(int Level, string ValueText, bool ShowDivider) { public string LevelText => Level.ToString(); }
    public sealed record AvailabilityRow(MultiRaceAvailabilityOption Option, string TotalText, Color RowBackgroundColor, Color BorderColor, double BorderThickness) { public string Display => Option.Display; }
    public sealed record LevelRow(int Level, string Name, string Detail, string AbilityRef, Color RowBackgroundColor) { public string LevelText => Level.ToString(); public bool HasDetails => !string.IsNullOrWhiteSpace(AbilityRef) || !string.IsNullOrWhiteSpace(Detail); }
    public sealed class OptionRow(string name) : INotifyPropertyChanged { public string Name { get; } = name; private bool _selected; public bool IsSelected { get => _selected; set { if (_selected == value) return; _selected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); PropertyChanged?.Invoke(this, new(nameof(RadioGlyph))); } } public string RadioGlyph => IsSelected ? FontAwesomeGlyphs.RadioSelected : FontAwesomeGlyphs.Circle; public event PropertyChangedEventHandler? PropertyChanged; }
}
