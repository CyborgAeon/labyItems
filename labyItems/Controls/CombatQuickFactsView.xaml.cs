namespace labyItems.Controls;

public partial class CombatQuickFactsView : ContentView
{
    public static readonly BindableProperty FactsProperty = BindableProperty.Create(
        nameof(Facts), typeof(CombatQuickFactsVm), typeof(CombatQuickFactsView), CombatQuickFactsVm.Empty);

    public CombatQuickFactsVm Facts
    {
        get => (CombatQuickFactsVm)GetValue(FactsProperty);
        set => SetValue(FactsProperty, value);
    }

    public CombatQuickFactsView() => InitializeComponent();
}

public sealed record CombatQuickFactsVm(string ValueLabel, string ValueText, string TypeText, string ArmourText, bool IsWide)
{
    public static CombatQuickFactsVm Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, false);
    public bool HasValue => ValueText.Length > 0;
    public bool HasType => TypeText.Length > 0;
    public bool HasArmour => ArmourText.Length > 0;
    public bool ShowTwoCells => HasValue && !IsWide && HasType && !HasArmour;
    public bool ShowThreeCells => HasValue && !IsWide && HasType && HasArmour;
    public bool ShowWideOnly => HasValue && (IsWide || !HasType) && !HasArmour;
    public bool ShowWideWithArmour => HasValue && (IsWide || !HasType) && HasArmour;
}
