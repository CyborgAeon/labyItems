using labyItems.Models.WorldMap;
using labyItems.Helpers;
using labyItems.Services;

namespace labyItems.Pages.WorldMap;

public partial class PrimusMapPage : ContentPage
{
    private readonly IWorldRegionRepository repository;
    private IReadOnlyList<WorldRegion> regions = [];
    private bool initialized;
    private bool showingRegion;

    public PrimusMapPage() : this(WorldRegionRepository.CreatePublic()) { }
    public PrimusMapPage(IWorldRegionRepository repository)
    {
        InitializeComponent();
        this.repository = repository;
        Map.RegionSelected += OnRegionSelected;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (initialized) return;
        try
        {
            regions = await repository.GetRegionsAsync();
            PeriodPicker.ItemsSource = repository.GetPeriods().ToList();
            PeriodPicker.SelectedIndex = PeriodPicker.Items.Count > 0 ? 0 : -1;
            PlacePicker.ItemsSource = repository.GetStatisticsRegions().ToList();
            StatusLabel.Text = await FileSystem.AppPackageFileExistsAsync("primus-map.png")
                ? "Pinch to zoom, drag to pan, tap a territory or location."
                : "Map artwork unavailable. Explore boundaries or browse monthly statistics.";
            await Map.LoadAsync();
            initialized = true;
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("WORLD_MAP", "Failed loading bundled world map data.", ex);
            StatusLabel.Text = "Unable to load world map data. Reopen this page to retry.";
        }
    }

    private async void OnRegionSelected(object? sender, string id)
    {
        var region = regions.FirstOrDefault(r => r.Id == id);
        if (region != null) await ShowRegionAsync(region);
    }

    private async void OnViewStatisticsClicked(object? sender, EventArgs e)
    {
        if (PlacePicker.SelectedItem is string name)
            await ShowRegionAsync(new WorldRegion { Id = name, Name = name, Category = "Published place", StatisticsId = name });
        else await DisplayAlertAsync("World statistics", "Choose a published place first.", "OK");
    }

    private async Task ShowRegionAsync(WorldRegion region)
    {
        if (showingRegion || Navigation.ModalStack.Count > 0 || PeriodPicker.SelectedItem is not WorldStatsPeriod period) return;
        showingRegion = true;
        try
        {
            var statistics = region.StatisticsId is null ? [] : repository.GetStatistics(region.StatisticsId, period);
            var modal = new NavigationPage(new RegionDetailsPage(region, period, statistics, modal: true, repository: repository));
            await Navigation.PushModalAsync(modal);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("WORLD_MAP", "Failed opening region statistics.", ex);
            await DisplayAlertAsync("World statistics", "Unable to open this region's statistics.", "OK");
        }
        finally { showingRegion = false; }
    }
}
