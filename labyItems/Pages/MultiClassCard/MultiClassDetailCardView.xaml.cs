using System.Collections.ObjectModel;
using labyItems.Helpers;
using labyItems.Services;

namespace labyItems.Pages.MultiClassCard;
public partial class MultiClassDetailCardView : ContentView
{
    public static readonly BindableProperty DefinitionProperty = BindableProperty.Create(nameof(Definition), typeof(MultiClassDefinition), typeof(MultiClassDetailCardView), propertyChanged: (bindable, oldValue, newValue) => ((MultiClassDetailCardView)bindable).Rebuild());
    public MultiClassDefinition? Definition { get => (MultiClassDefinition?)GetValue(DefinitionProperty); set => SetValue(DefinitionProperty, value); }
    public ObservableCollection<LevelRow> LevelRows { get; } = new();
    public ObservableCollection<AvailabilityRow> AvailabilityRows { get; } = new();
    public string DisplayName => Definition?.DisplayName ?? string.Empty;
    public string IconGlyph => Definition?.IconGlyph ?? string.Empty;
    public bool IsBracketPure => !string.IsNullOrWhiteSpace(Definition?.RequiresBracketPure);
    public string BracketGlyph => (Definition?.RequiresBracketPure ?? string.Empty).Contains("Warrior", StringComparison.OrdinalIgnoreCase)
        ? "🛡️"
        : ClassBracketIconHelper.GetBracketGlyph(Definition?.RequiresBracketPure);
    public bool HasAvailability => AvailabilityRows.Count > 0;
    public MultiClassDetailCardView() { InitializeComponent(); BindingContext = this; }
    private void Rebuild()
    {
        LevelRows.Clear(); AvailabilityRows.Clear();
        if (Definition != null)
        {
            var keys = Definition.Levels.Keys.Concat(Definition.SystemEffectsByLevel.Keys).Select(ParseLevel).Where(x => x > 0).Distinct().OrderBy(x => x);
            foreach (var level in keys) { foreach (var ability in Definition.Levels.GetValueOrDefault(level.ToString()) ?? []) LevelRows.Add(new(level, ability.Name, ability.Effect, ability.AbilityRef)); foreach (var effect in Definition.SystemEffectsByLevel.GetValueOrDefault(level.ToString()) ?? []) if (!string.IsNullOrWhiteSpace(effect.Display)) LevelRows.Add(new(level, effect.Display, effect.Display, string.Empty)); }
            foreach (var item in Definition.AvailabilityOptions) AvailabilityRows.Add(new(item.Display, Cost(item,1), Cost(item,2), Cost(item,3), Cost(item,4), Cost(item,5), Cost(item,6)));
        }
        OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(IconGlyph)); OnPropertyChanged(nameof(IsBracketPure)); OnPropertyChanged(nameof(BracketGlyph)); OnPropertyChanged(nameof(HasAvailability));
    }
    private async void OnLevelInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: LevelRow row }) return;
        if (!string.IsNullOrWhiteSpace(row.AbilityRef))
        {
            var ability = await DetailCardLookupService.ResolveAbilityAsync(row.AbilityRef);
            if (ability != null) { await Navigation.PushAsync(new Pages.AbilityCard.AbilityCard(ability)); return; }
        }
        await Shell.Current.DisplayAlert(row.Name, string.IsNullOrWhiteSpace(row.Detail) ? "No further information available." : row.Detail, "OK");
    }
    private static int ParseLevel(string value) => int.TryParse(value, out var result) ? result : 0;
    private static string Cost(MultiClassAvailabilityOption item, int level) => item.CostsByLevel.TryGetValue(level.ToString(), out var cost) ? cost.ToString() : "—";
    public sealed record LevelRow(int Level, string Name, string Detail, string AbilityRef);
    public sealed record AvailabilityRow(string Display,string L1,string L2,string L3,string L4,string L5,string L6);
}
