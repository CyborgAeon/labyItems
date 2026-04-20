namespace labyItems.Pages.Calendar;

public partial class AreaMapPage : ContentPage
{
    public AreaMapPage(string areaCode)
    {
        InitializeComponent();
        TitleLabel.Text = $"Area {areaCode} Map";
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
