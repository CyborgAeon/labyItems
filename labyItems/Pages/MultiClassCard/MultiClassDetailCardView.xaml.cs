using System.Collections.ObjectModel;
using System.Text.Json;
using labyItems.Controls;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.MultiClassCard;

public partial class MultiClassDetailCardView : ContentView
{
    public static readonly BindableProperty DefinitionProperty = BindableProperty.Create(nameof(Definition), typeof(MultiClassDefinition), typeof(MultiClassDetailCardView), propertyChanged: (b, _, _) => ((MultiClassDetailCardView)b).Rebuild());
    public MultiClassDefinition? Definition { get => (MultiClassDefinition?)GetValue(DefinitionProperty); set => SetValue(DefinitionProperty, value); }
    public ObservableCollection<LevelRow> LevelRows { get; } = new();
    public ObservableCollection<AvailabilityRow> AvailabilityRows { get; } = new();
    public ObservableCollection<ValueCell> CostCells { get; } = new();
    public ObservableCollection<ValueCell> LifeCells { get; } = new();
    private MultiClassAvailabilityOption? _selectedAvailability;

    public string DisplayName => Definition?.DisplayName ?? string.Empty;
    public string IconGlyph => Definition?.IconGlyph ?? string.Empty;
    public bool IsBracketPure => !string.IsNullOrWhiteSpace(Definition?.RequiresBracketPure);
    public bool RequiresPath => Definition?.AvailabilityOptions?.Any(x => NotesText(x.Notes).Contains("Path", StringComparison.OrdinalIgnoreCase)) == true;
    public string LevelCapText => (Definition?.MaxLevel ?? 0).ToString();
    public string PreReqsText => Join(_selectedAvailability?.PreReqs) is { Length: > 0 } value ? value : "None";
    public string PowerText => Definition == null ? "—" : Join(Definition.SystemEffectsByLevel.Values.SelectMany(x => x ?? []).Where(x => x.EffectType.Contains("Power", StringComparison.OrdinalIgnoreCase)).Select(x => x.Display).Distinct(StringComparer.OrdinalIgnoreCase)) is { Length: > 0 } value ? value : "—";
    public string MaxAcText => Definition == null ? "—" : Join(Definition.SystemEffectsByLevel.Values.SelectMany(x => x ?? []).Where(x => x.EffectType.Equals("MaxAcOverride", StringComparison.OrdinalIgnoreCase)).Select(x => x.Display).Distinct(StringComparer.OrdinalIgnoreCase));
    public bool HasMaxAc => !string.IsNullOrWhiteSpace(MaxAcText) && MaxAcText != "—";
    public bool HasLifeScale => LifeCells.Count > 0;
    public bool HasAvailability => AvailabilityRows.Count > 0;
    public bool HasLevels => LevelRows.Count > 0;
    public string SelectedAvailabilityText => _selectedAvailability?.Display ?? "No availability data";

    public MultiClassDetailCardView() { InitializeComponent(); BindingContext = this; }

    private void Rebuild()
    {
        LevelRows.Clear(); AvailabilityRows.Clear(); CostCells.Clear(); LifeCells.Clear();
        var definition = Definition;
        if (definition != null)
        {
            _selectedAvailability = (definition.AvailabilityOptions ?? []).OrderBy(TotalCost).ThenBy(x => x.Display, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            foreach (var option in definition.AvailabilityOptions ?? []) AvailabilityRows.Add(new(option.Display, $"{TotalCost(option)} pts", RowColor(AvailabilityRows.Count)));
            foreach (var level in definition.Levels.Keys.Select(ParseLevel).Where(x => x > 0).OrderBy(x => x))
                foreach (var ability in definition.Levels.GetValueOrDefault(level.ToString()) ?? []) LevelRows.Add(new(level, ability.Name, ability.Effect, ability.AbilityRef, RowColor(LevelRows.Count)));
            foreach (var level in Enumerable.Range(1, Math.Clamp(definition.MaxLevel, 0, 8)))
            {
                var life = (definition.SystemEffectsByLevel.GetValueOrDefault(level.ToString()) ?? []).FirstOrDefault(x => x.EffectType.Equals("BaseLife", StringComparison.OrdinalIgnoreCase));
                if (life != null) LifeCells.Add(new(level, life.Display));
            }
            BuildCostCells();
        }
        RaiseAll();
    }

    private void BuildCostCells()
    {
        CostCells.Clear();
        for (var level = 1; level <= Math.Clamp(Definition?.MaxLevel ?? 0, 0, 8); level++)
            CostCells.Add(new(level, _selectedAvailability?.CostsByLevel.GetValueOrDefault(level.ToString()).ToString() ?? "—"));
    }

    private async void OnCostTapped(object? sender, TappedEventArgs e)
    {
        var options = Definition?.AvailabilityOptions ?? [];
        if (options.Count <= 1 || GetParentPage() is not Page page) return;
        var labels = options.Select(x => $"{x.Display} ({TotalCost(x)} pts total)").ToArray();
        var chosen = await page.DisplayActionSheet("Select availability", "Cancel", null, labels);
        var index = Array.IndexOf(labels, chosen);
        if (index < 0) return;
        _selectedAvailability = options[index]; BuildCostCells(); RaiseAll();
    }

    private async void OnLevelInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not ListItemRowView { ActionCommandParameter: LevelRow row } || GetParentPage() is not Page page) return;
        var detail = await DetailCardLookupService.ResolveDetailAsync(row.AbilityRef, null, row.Name);
        var ability = detail?.Ability ?? new EvolutionService.AbilityResult { Index = row.Name, DisplayName = row.Name, Description = row.Detail, AbilityRef = row.AbilityRef, Available = "Multi-class" };
        await page.Navigation.PushAsync(new Pages.AbilityCard.AbilityCard(ability));
    }

    private void RaiseAll() { foreach (var name in new[] { nameof(DisplayName), nameof(IconGlyph), nameof(IsBracketPure), nameof(RequiresPath), nameof(LevelCapText), nameof(PreReqsText), nameof(PowerText), nameof(MaxAcText), nameof(HasMaxAc), nameof(HasLifeScale), nameof(HasAvailability), nameof(HasLevels), nameof(SelectedAvailabilityText) }) OnPropertyChanged(name); }
    private Page? GetParentPage() { Element? current = this; while (current != null) { if (current is Page page) return page; current = current.Parent; } return null; }
    private static int TotalCost(MultiClassAvailabilityOption option) => option.CostsByLevel?.Values.Sum() ?? 0;
    private static int ParseLevel(string value) => int.TryParse(value, out var result) ? result : 0;
    private static string Join(IEnumerable<string>? values) => string.Join(", ", values?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? []);
    private static string NotesText(JsonElement value) => value.ValueKind switch { JsonValueKind.String => value.GetString() ?? string.Empty, JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(x => x.ToString())), _ => string.Empty };
    private static Color RowColor(int index) => index % 2 == 0 ? Colors.White : Color.FromArgb("#FAF8F3");
    public sealed record ValueCell(int Level, string Value) { public string LevelText => $"L{Level}"; public string CostText => Value; }
    public sealed record AvailabilityRow(string Display, string TotalText, Color RowBackgroundColor);
    public sealed record LevelRow(int Level, string Name, string Detail, string AbilityRef, Color RowBackgroundColor) { public string LevelText => Level.ToString(); public bool HasDetails => !string.IsNullOrWhiteSpace(AbilityRef) || !string.IsNullOrWhiteSpace(Detail); }
}
