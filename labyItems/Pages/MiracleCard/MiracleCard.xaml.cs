using labyItems.Services;

namespace labyItems.Pages.MiracleCard;

public partial class MiracleCard : ContentPage
{
    public MiracleCard()
    {
        InitializeComponent();
    }

    public MiracleCard(MiracleService.MiracRaw miracle)
        : this()
    {
        MiracleDetails.Miracle = miracle;
        Title = string.IsNullOrWhiteSpace(miracle?.name) ? "Miracle" : miracle.name;
    }
}
