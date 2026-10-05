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
        Title = "Oraculum Insight";
        DetailHeader.HeaderText = Title;
    }
}
