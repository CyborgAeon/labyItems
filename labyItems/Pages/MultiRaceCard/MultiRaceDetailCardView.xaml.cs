using labyItems.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Services;
using labyItems.Controls;
using System.Text.Json;

namespace labyItems.Pages.MultiRaceCard;

public partial class MultiRaceDetailCardView : ContentView
{
    public static readonly BindableProperty DefinitionProperty = BindableProperty.Create(nameof(Definition), typeof(MultiRaceDefinition), typeof(MultiRaceDetailCardView), propertyChanged: (bindable, oldValue, newValue) => ((MultiRaceDetailCardView)bindable).Rebuild());
    public MultiRaceDefinition? Definition { get => (MultiRaceDefinition?)GetValue(DefinitionProperty); set => SetValue(DefinitionProperty, value); }
    public ObservableCollection<OptionRow> Options { get; } = new();
    public ObservableCollection<LevelRow> LevelRows { get; } = new();
    public ObservableCollection<AvailabilityRow> AvailabilityRows { get; } = new();
    public string DisplayName => Definition?.DisplayName ?? string.Empty;
    public string IconGlyph => Definition?.IconGlyph ?? string.Empty;
    public bool HasOptions => Options.Count > 0;
    public bool HasAvailability => AvailabilityRows.Count > 0;
    public string NotesText => string.Join("\n\n", Definition?.Notes?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? []);
    public bool HasNotes => NotesText.Length > 0;
    public bool IsBracketPure => !string.IsNullOrWhiteSpace(Definition?.RequiresBracketPure);
    public bool RequiresPath => Definition?.AvailabilityOptions?.Any(x => ExtractNotesText(x.Notes).Contains("Path", StringComparison.OrdinalIgnoreCase)) == true;
    public string LevelCapText => (Definition?.MaxLevel ?? 0).ToString();
    public string PreReqsText => string.Join(", ", Definition?.AvailabilityOptions?.SelectMany(x => x.PreReqs ?? []).Distinct(StringComparer.OrdinalIgnoreCase) ?? []).Trim() is { Length: > 0 } value ? value : "None";
    public string PowerText => string.Join(", ", Definition?.SystemEffectsByLevel?.Values.SelectMany(x => x ?? []).Where(x => x.EffectType.Contains("Power", StringComparison.OrdinalIgnoreCase)).Select(x => x.Display).Distinct(StringComparer.OrdinalIgnoreCase) ?? []).Trim() is { Length: > 0 } value ? value : "—";
    private string? _selectedOption;

    public MultiRaceDetailCardView() { InitializeComponent(); BindingContext = this; }

    private void Rebuild()
    {
        Options.Clear(); AvailabilityRows.Clear();
        if (Definition != null)
        {
            foreach (var name in Definition.Levels.Values.SelectMany(x => x).SelectMany(x => x.PreReqs ?? []).Select(OptionFromPrerequisite).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)) Options.Add(new OptionRow(name));
            foreach (var item in Definition.AvailabilityOptions) AvailabilityRows.Add(new AvailabilityRow(item.Display, Cost(item,1), Cost(item,2), Cost(item,3), Cost(item,4), Cost(item,5), Cost(item,6), $"{item.CostsByLevel.Values.Sum()} pts"));
        }
        BuildLevels(); RaiseAll();
    }

    private void BuildLevels()
    {
        LevelRows.Clear(); if (Definition == null) return;
        var keys = Definition.Levels.Keys.Concat(Definition.SystemEffectsByLevel.Keys).Select(ParseLevel).Where(x => x > 0).Distinct().OrderBy(x => x);
        foreach (var level in keys)
        {
            var abilities = Definition.Levels.GetValueOrDefault(level.ToString()) ?? [];
            foreach (var ability in abilities.Where(IsVisibleForOption)) LevelRows.Add(new LevelRow(level, ability.Name, ability.Effect, ability.AbilityRef));
            foreach (var effect in Definition.SystemEffectsByLevel.GetValueOrDefault(level.ToString()) ?? []) if (!string.IsNullOrWhiteSpace(effect.Display)) LevelRows.Add(new LevelRow(level, effect.Display, effect.Display, string.Empty));
        }
    }

    private bool IsVisibleForOption(MultiRaceLevelAbility ability)
    {
        var choices = (ability.PreReqs ?? []).Select(OptionFromPrerequisite).Where(x => x.Length > 0).ToList();
        return choices.Count == 0 || _selectedOption == null || choices.Contains(_selectedOption, StringComparer.OrdinalIgnoreCase);
    }
    private void OnOptionTapped(object? sender, TappedEventArgs e) { if (e.Parameter is not OptionRow row) return; _selectedOption = string.Equals(_selectedOption,row.Name,StringComparison.OrdinalIgnoreCase) ? null : row.Name; foreach(var option in Options) option.IsSelected = string.Equals(option.Name,_selectedOption,StringComparison.OrdinalIgnoreCase); BuildLevels(); }
    private async void OnLevelInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not ListItemRowView { ActionCommandParameter: LevelRow row }) return;
        var ability = !string.IsNullOrWhiteSpace(row.AbilityRef) ? await DetailCardLookupService.ResolveAbilityAsync(row.AbilityRef) : null;
        ability ??= new EvolutionService.AbilityResult { Index = row.Name, DisplayName = row.Name, Description = row.Detail, AbilityRef = row.AbilityRef, Available = "Multi-race" };
        await Navigation.PushAsync(new Pages.AbilityCard.AbilityCard(ability));
    }
    private static string OptionFromPrerequisite(string value) { const string marker = ".type."; var index = value.IndexOf(marker,StringComparison.OrdinalIgnoreCase); return index < 0 ? string.Empty : string.Join(' ', value[(index+marker.Length)..].Split('-', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0])+x[1..])); }
    private static int ParseLevel(string value) => int.TryParse(value, out var result) ? result : 0;
    private static string Cost(MultiRaceAvailabilityOption item, int level) => item.CostsByLevel.TryGetValue(level.ToString(), out var cost) ? cost.ToString() : "—";
    private static string ExtractNotesText(JsonElement value) => value.ValueKind switch { JsonValueKind.String => value.GetString() ?? string.Empty, JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(x => x.ToString())), _ => string.Empty };
    private void RaiseAll() { foreach (var name in new[] { nameof(DisplayName), nameof(IconGlyph), nameof(HasOptions), nameof(HasAvailability), nameof(NotesText), nameof(HasNotes), nameof(IsBracketPure), nameof(RequiresPath), nameof(LevelCapText), nameof(PreReqsText), nameof(PowerText) }) OnPropertyChanged(name); }
    public sealed class OptionRow(string name) : INotifyPropertyChanged { public string Name { get; } = name; private bool _selected; public bool IsSelected { get => _selected; set { if(_selected==value)return; _selected=value; PropertyChanged?.Invoke(this,new(nameof(IsSelected))); PropertyChanged?.Invoke(this,new(nameof(RadioGlyph))); } } public string RadioGlyph => IsSelected ? FontAwesomeGlyphs.RadioSelected : FontAwesomeGlyphs.Circle; public event PropertyChangedEventHandler? PropertyChanged; }
    public sealed record LevelRow(int Level, string Name, string Detail, string AbilityRef) { public string LevelText => Level.ToString(); public bool HasDetails => !string.IsNullOrWhiteSpace(AbilityRef) || !string.IsNullOrWhiteSpace(Detail); }
    public sealed record AvailabilityRow(string Display,string L1,string L2,string L3,string L4,string L5,string L6,string TotalText);
}
