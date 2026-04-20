using System.Collections.ObjectModel;
using labyItems.Services;

namespace labyItems.Pages.AbilityCard;

public partial class AbilityCardComparisonPage : ContentPage
{
    public ObservableCollection<AbilityCardComparisonEntryVm> Cards { get; } = new();
    public string TitleText { get; }

    public AbilityCardComparisonPage(string title, IReadOnlyList<EvolutionService.AbilityResult> abilities)
    {
        InitializeComponent();

        TitleText = string.IsNullOrWhiteSpace(title)
            ? "Ability Details"
            : title.Trim();

        foreach (var ability in abilities ?? Array.Empty<EvolutionService.AbilityResult>())
        {
            if (ability == null)
                continue;

            Cards.Add(new AbilityCardComparisonEntryVm(ability));
        }

        BindingContext = this;
        Title = Cards.Count <= 1 ? TitleText : $"{Cards.Count} Ability Details";
    }
}

public sealed record AbilityCardComparisonEntryVm(EvolutionService.AbilityResult Ability);
