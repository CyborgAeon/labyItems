using labyItems.Services;
namespace labyItems.Pages.MultiClassCard;
public partial class MultiClassCard : ContentPage
{
    public MultiClassCard() => InitializeComponent();
    public MultiClassCard(MultiClassDefinition definition, string? key = null) : this() { Details.Definition = definition; Title = string.IsNullOrWhiteSpace(definition.DisplayName) ? key ?? "Multi-Class Details" : definition.DisplayName; DetailHeader.HeaderText = Title; }
}
