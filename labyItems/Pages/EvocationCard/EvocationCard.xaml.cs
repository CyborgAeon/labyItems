using labyItems.Services;

namespace labyItems.Pages.EvocationCard;

public partial class EvocationCard : ContentPage
{
    public EvocationCard()
    {
        InitializeComponent();
    }

    public EvocationCard(DruidEvocationService.EvocRaw evocation)
        : this()
    {
        EvocationDetails.Evocation = evocation;
        Title = string.IsNullOrWhiteSpace(evocation?.name) ? "Evocation" : evocation.name;
    }
}
