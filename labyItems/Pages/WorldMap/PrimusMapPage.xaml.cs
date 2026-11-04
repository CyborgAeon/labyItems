using System.Globalization;
using labyItems.Controls;
using labyItems.Models.WorldMap;
using labyItems.Helpers;
using labyItems.Services;

namespace labyItems.Pages.WorldMap;

public partial class PrimusMapPage : ContentPage
{
    private readonly IWorldRegionRepository repository;
    private IReadOnlyList<WorldRegion> regions = [];
    private IReadOnlyList<WorldStatsPeriod> periods = [];
    private IReadOnlyList<WorldTownResult> towns = [];
    private WorldRegion? selectedRegion;
    private WorldStatsPeriod? period;
    private WorldMapFilter filters = WorldMapFilter.Empty;
    private bool initialized;
    private bool showingRegion;

    public PrimusMapPage() : this(WorldRegionRepository.CreatePublic()) { }
    public PrimusMapPage(IWorldRegionRepository repository)
    {
        InitializeComponent();
        this.repository = repository;
        Map.RegionSelectionChanged += (_, id) => RunOnUi(async () =>
        {
            var region = regions.FirstOrDefault(r => r.Id == id);
            if (region is not null) { selectedRegion = region; RefreshOverview(); }
            await Task.CompletedTask;
        });
        Map.RegionDetailsRequested += (_, id) => RunOnUi(async () =>
        {
            var region = regions.FirstOrDefault(r => r.Id == id);
            if (region is not null) await ShowRegionAsync(region);
        });
        SizeChanged += (_, _) =>
        {
            FilterPanel.WidthRequest = Math.Max(0, Math.Min(420, Width - 32));
            FilterPanel.HeightRequest = Math.Max(0, Math.Min(560, Height - 48));
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (initialized) return;
        await RunAsync(async () =>
        {
            regions = await repository.GetRegionsAsync();
            periods = repository.GetPeriods();
            period = periods.FirstOrDefault();
            LoadTowns();
            selectedRegion = towns.FirstOrDefault()?.Region;
            RefreshOverview();
            await Map.LoadAsync();
            if (selectedRegion is not null) await Map.SelectRegionAsync(selectedRegion.Id, focus: false);
            await ApplyFiltersAsync();
            initialized = true;
        });
    }

    private void LoadTowns()
    {
        towns = period is null ? [] : repository.GetStatisticsRegions().Select(id =>
        {
            var region = regions.FirstOrDefault(r => r.StatisticsId == id && r.Category == "location")
                ?? regions.FirstOrDefault(r => r.StatisticsId == id)
                ?? new WorldRegion { Id = id, Name = id, Category = "Published place", StatisticsId = id };
            return new WorldTownResult(region, repository.GetStatistics(id, period));
        }).Where(town => town.Statistics.Count > 0).ToList();
    }

    private void RefreshOverview()
    {
        HeaderDate.Text = period?.ToString() ?? "No published month";
        ToolbarDate.Text = period is null ? "Month" : new DateTime(period.Year, period.Month, 1).ToString("MMM yyyy");
        var groups = Groups(selectedRegion);
        OverviewPlace.Text = selectedRegion is null ? "Choose a territory or location" : $"{selectedRegion.Name} · {selectedRegion.Category}";
        DeityHighlight.Text = CardDeity.Text = WorldStatisticsSummaryFormatter.HighestText(groups, "Deities and Orders");
        AdministrationHighlight.Text = CardAdministration.Text = WorldStatisticsSummaryFormatter.HighestText(groups, "Administration");
        DragonHighlight.Text = CardDragon.Text = WorldStatisticsSummaryFormatter.HighestText(groups, "Dragons");
        SelectedPlaceCard.IsVisible = selectedRegion is not null;
        SelectedName.Text = selectedRegion?.Name ?? "";
        SelectedCategory.Text = selectedRegion is null ? "" : $"{selectedRegion.Category} · {period}";
    }

    private IReadOnlyList<WorldStatisticsGroup> Groups(WorldRegion? region)
    {
        if (region is null || period is null) return [];
        var ids = region.StatisticsId is not null ? new[] { region.StatisticsId } : region.RelatedStatisticsIds ?? [];
        return ids.Select(id => new WorldStatisticsGroup(id, repository.GetStatistics(id, period))).ToList();
    }

    private void OnMapSizeChanged(object? sender, EventArgs e)
    {
        // The original landscape artwork fills the width without stretching or letterboxing.
        if (Map.Width > 0 && Math.Abs(Map.HeightRequest - Map.Width * .75) > .5)
            Map.HeightRequest = Map.Width * .75;
    }

    private void OnZoomInClicked(object? sender, EventArgs e) => RunOnUi(Map.ZoomInAsync);
    private void OnZoomOutClicked(object? sender, EventArgs e) => RunOnUi(Map.ZoomOutAsync);
    private void OnFitClicked(object? sender, EventArgs e) => RunOnUi(Map.FitAsync);
    private void OnOpenSelectedClicked(object? sender, EventArgs e) => RunOnUi(async () => { if (selectedRegion is not null) await ShowRegionAsync(selectedRegion); });
    private void OnSelectDateClicked(object? sender, EventArgs e) => RunOnUi(async () =>
    {
        var choice = await DisplayActionSheetAsync("Statistics month", "Cancel", null, periods.Select(p => p.ToString()).ToArray());
        var selected = periods.FirstOrDefault(p => p.ToString() == choice);
        if (selected is null) return;
        period = selected;
        LoadTowns();
        RefreshOverview();
        await ApplyFiltersAsync();
    });

    private void OnSelectPlaceClicked(object? sender, EventArgs e) => RunOnUi(async () =>
    {
        var options = regions.Concat(towns.Where(town => !regions.Any(r => r.Id == town.Region.Id)).Select(town => town.Region))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Category, StringComparer.Ordinal).ToList();
        var labels = options.Select(r => $"{r.Name} ({r.Category})").ToArray();
        var choice = await DisplayActionSheetAsync("Select place", "Cancel", null, labels);
        var index = Array.IndexOf(labels, choice);
        if (index < 0) return;
        await SelectRegionAsync(options[index]);
    });

    private async Task SelectRegionAsync(WorldRegion region)
    {
        selectedRegion = region;
        RefreshOverview();
        await Map.SelectRegionAsync(region.Id, focus: true);
    }

    private void OnTownResultClicked(object? sender, EventArgs e) => RunOnUi(async () =>
    {
        if (sender is ListItemRowView { ActionCommandParameter: WorldTownResult town })
        {
            await SelectRegionAsync(town.Region);
        }
    });

    private void OnTownInfoClicked(object? sender, EventArgs e) => RunOnUi(async () =>
    {
        if (sender is ListItemRowView { ActionCommandParameter: WorldTownResult town })
        { await SelectRegionAsync(town.Region); await ShowRegionAsync(town.Region); }
    });

    private async Task ShowRegionAsync(WorldRegion region)
    {
        if (showingRegion || Navigation.ModalStack.Count > 0 || period is null) return;
        showingRegion = true;
        try { await Navigation.PushModalAsync(new NavigationPage(new RegionDetailsPage(region, period, repository))); }
        finally { showingRegion = false; }
    }

    private void OnOpenFiltersClicked(object? sender, EventArgs e)
    {
        NameFilter.Text = filters.Query;
        var stats = towns.SelectMany(t => t.Statistics).ToList();
        PopulateMetric(AdministrationMetric, "Administration", stats);
        PopulateMetric(DragonMetric, "Dragons", stats);
        PopulateMetric(DeityMetric, "Deities and Orders", stats);
        FillRange(HallMinimum, HallMaximum, filters.Criteria.FirstOrDefault(c => c.Category == "Administration" && c.Name == "Volksmight"));
        FillRange(AdministrationMinimum, AdministrationMaximum, filters.Criteria.FirstOrDefault(c => c.Category == "Administration" && c.Name != "Volksmight"));
        FillRange(DragonMinimum, DragonMaximum, filters.Criteria.FirstOrDefault(c => c.Category == "Dragons"));
        FillRange(DeityMinimum, DeityMaximum, filters.Criteria.FirstOrDefault(c => c.Category == "Deities and Orders"));
        FilterError.IsVisible = false;
        FilterOverlay.IsVisible = true;
        TownResults.InputTransparent = true;
    }

    private void PopulateMetric(Picker picker, string category, IReadOnlyList<WorldStatistic> stats)
    {
        picker.ItemsSource = new[] { "Highest published value" }.Concat(stats.Where(s => s.Category == category && !(category == "Administration" && s.Name == "Volksmight"))
            .Select(s => s.Name).Distinct().OrderBy(name => name, StringComparer.Ordinal)).ToList();
        var selected = filters.Criteria.FirstOrDefault(c => c.Category == category && !(category == "Administration" && c.Name == "Volksmight"))?.Name;
        picker.SelectedIndex = selected is null ? 0 : picker.Items.IndexOf(selected);
    }

    private static void FillRange(Entry minimum, Entry maximum, WorldStatisticFilter? criterion)
    { minimum.Text = criterion?.Minimum?.ToString(CultureInfo.CurrentCulture); maximum.Text = criterion?.Maximum?.ToString(CultureInfo.CurrentCulture); }

    private void OnCancelFiltersClicked(object? sender, EventArgs e) => CloseFilters();
    private void OnCancelFiltersTapped(object? sender, TappedEventArgs e) => CloseFilters();
    private void CloseFilters() { FilterOverlay.IsVisible = false; TownResults.InputTransparent = false; }
    protected override bool OnBackButtonPressed() { if (FilterOverlay.IsVisible) { CloseFilters(); return true; } return base.OnBackButtonPressed(); }
    private void OnResetFiltersClicked(object? sender, EventArgs e)
    {
        NameFilter.Text = "";
        foreach (var entry in new[] { HallMinimum, HallMaximum, AdministrationMinimum, AdministrationMaximum, DragonMinimum, DragonMaximum, DeityMinimum, DeityMaximum }) entry.Text = "";
        AdministrationMetric.SelectedIndex = DragonMetric.SelectedIndex = DeityMetric.SelectedIndex = 0;
        FilterError.IsVisible = false;
    }

    private void OnApplyFiltersClicked(object? sender, EventArgs e) => RunOnUi(async () =>
    {
        try
        {
            var criteria = new List<WorldStatisticFilter>();
            AddRange(criteria, "Administration", "Volksmight", HallMinimum, HallMaximum);
            AddRange(criteria, "Administration", SelectedMetric(AdministrationMetric), AdministrationMinimum, AdministrationMaximum);
            AddRange(criteria, "Dragons", SelectedMetric(DragonMetric), DragonMinimum, DragonMaximum);
            AddRange(criteria, "Deities and Orders", SelectedMetric(DeityMetric), DeityMinimum, DeityMaximum);
            filters = new WorldMapFilter(NameFilter.Text?.Trim() ?? "", criteria);
        }
        catch (FormatException ex) { FilterError.Text = ex.Message; FilterError.IsVisible = true; return; }
        CloseFilters();
        await ApplyFiltersAsync();
    });

    private static string? SelectedMetric(Picker picker) => picker.SelectedIndex <= 0 ? null : picker.SelectedItem as string;
    private static void AddRange(List<WorldStatisticFilter> criteria, string category, string? name, Entry minimumEntry, Entry maximumEntry)
    {
        decimal? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var number) || number < 0)
                throw new FormatException("Enter a non-negative score in each limit, or leave it blank.");
            return number;
        }
        var minimum = Parse(minimumEntry.Text); var maximum = Parse(maximumEntry.Text);
        if (minimum is null && maximum is null) return;
        if (minimum is not null && maximum is not null && minimum > maximum)
            throw new FormatException("Minimum scores cannot exceed maximum scores.");
        criteria.Add(new(category, name, minimum, maximum));
    }

    private async Task ApplyFiltersAsync()
    {
        var matches = WorldMapFilterEvaluator.Apply(towns, filters);
        TownResults.ItemsSource = filters.IsActive ? matches : Array.Empty<WorldTownResult>();
        ResultsHeading.IsVisible = filters.IsActive;
        ResultsHeading.Text = $"{matches.Count} matching towns · {period}";
        EmptyResults.IsVisible = filters.IsActive && matches.Count == 0;
        PlacesButton.Text = filters.IsActive ? $"{matches.Count} towns" : "All places";
        SemanticProperties.SetDescription(FilterButton, filters.IsActive ? "Edit active town filters" : "Filter towns");
        await Map.SetMatchingRegionsAsync(filters.IsActive ? matches.Select(t => t.Region.Id) : null);
        if (filters.IsActive && matches.Count == 0)
        {
            selectedRegion = null;
            RefreshOverview();
            await Map.SelectRegionAsync("", focus: false);
        }
        else if (matches.Count > 0 && (selectedRegion is null || filters.IsActive && !matches.Any(t => t.Region.Id == selectedRegion.Id)))
            await SelectRegionAsync(matches[0].Region);
    }

    private async void RunOnUi(Func<Task> action) => await RunAsync(action);
    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); StatusLabel.IsVisible = false; }
        catch (Exception ex)
        {
            RuntimeLog.Write("WORLD_MAP", "Failed updating world map.", ex);
            StatusLabel.Text = "Unable to load the selected map data. Please try again.";
            StatusLabel.IsVisible = true;
        }
    }
}
