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
        Title = "Words From Above";
        DetailHeader.HeaderText = Title;
    }
}
