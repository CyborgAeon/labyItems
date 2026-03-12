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

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (Navigation?.ModalStack?.Count > 0)
        {
            await Navigation.PopModalAsync();
            return;
        }

        if (Navigation?.NavigationStack?.Count > 1)
            await Navigation.PopAsync();
    }
}
