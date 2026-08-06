using labyItems.Services;

namespace labyItems.Pages.NeuronicCard;

public partial class NeuronicCard : ContentPage
{
    public NeuronicCard()
    {
        InitializeComponent();
    }

    public NeuronicCard(NeuronicService.NeuronicRaw neuronic)
        : this()
    {
        NeuronicDetails.Neuronic = neuronic;
        Title = string.IsNullOrWhiteSpace(neuronic?.name) ? "Neuronic" : neuronic.name;
    }
}
