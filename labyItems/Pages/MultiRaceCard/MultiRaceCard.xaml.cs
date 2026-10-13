using labyItems.Services;

namespace labyItems.Pages.MultiRaceCard;

public partial class MultiRaceCard : ContentPage
{
    public MultiRaceCard() => InitializeComponent();

    public MultiRaceCard(MultiRaceDefinition definition, string? key = null) : this()
    {
        Details.Definition = definition;
        Title = string.IsNullOrWhiteSpace(definition.DisplayName) ? key ?? "Multi-Race Details" : definition.DisplayName;
        DetailHeader.HeaderText = Title;
    }
}
