using labyItems.Services;

namespace labyItems.Pages.Calendar;

public partial class MyBookingsPage : ContentPage
{
    private readonly long _accountVersion = LabyrintheSession.Shared.Version;
    private readonly CalendarEventStore _eventStore;
    private readonly CalendarStatusLabelUpdater _statusLabel;
    private bool _isLoading;

    public MyBookingsPage()
    {
        InitializeComponent();
        _eventStore = ServiceHelper.ResolveService<CalendarEventStore>() ?? CalendarEventStore.Shared;
        _statusLabel = new CalendarStatusLabelUpdater(LastUpdatedLabel, () => _eventStore.BookingsStatus, OnCalendarDateChangedAsync);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _eventStore.Refreshed -= OnStoreRefreshed;
        _eventStore.Refreshed += OnStoreRefreshed;
        _statusLabel.Start();
        await LoadBookingsAsync();
    }

    private async Task OnCalendarDateChangedAsync()
    {
        await _eventStore.ExpireAsync();
        await LoadBookingsAsync();
    }

    protected override void OnDisappearing()
    {
        _eventStore.Refreshed -= OnStoreRefreshed;
        _statusLabel.Stop();
        base.OnDisappearing();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void OnStoreRefreshed() => Dispatcher.Dispatch(() =>
    {
        if (_accountVersion != LabyrintheSession.Shared.Version || Window == null) return;
        RenderBookings(_eventStore.GetBookedEvents());
        _statusLabel.Update();
    });

    private void RenderBookings(IReadOnlyList<labyItems.Models.Calendar.CalendarEventRecord> bookedEvents)
    {
        BookingsHost.Children.Clear();
        CalendarVisualHelper.PopulateLegend(LegendHost);
        if (bookedEvents.Count == 0)
        {
            BookingsHost.Children.Add(new Label
            {
                Text = _eventStore.BookingsStatus == null
                    ? "No saved bookings are available. Connect to load your bookings."
                    : "You are not currently booked onto any events.",
                FontAttributes = FontAttributes.Italic,
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }
        foreach (var evt in bookedEvents)
        {
            BookingsHost.Children.Add(CalendarVisualHelper.BuildEventCard(evt, async () =>
            {
                await Navigation.PushAsync(new EventDetailsPage(evt.Id));
            }));
        }
    }

    private async Task LoadBookingsAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        BookingsLoadingIndicator.IsVisible = BookingsLoadingIndicator.IsRunning = true;
        BookingsHost.Children.Clear();
        try
        {
            var bookedEvents = await _eventStore.ReadBookingsAndRefreshAsync();
            if (_accountVersion != LabyrintheSession.Shared.Version) return;
            RenderBookings(bookedEvents);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            if (_accountVersion != LabyrintheSession.Shared.Version) return;
            await DisplayAlert("Unable to load bookings", ex.Message, "OK");
            BookingsHost.Children.Add(new Label { Text = "Bookings could not be loaded. Please try again." });
        }
        finally
        {
            _isLoading = false;
            _statusLabel.Update();
            BookingsLoadingIndicator.IsVisible = BookingsLoadingIndicator.IsRunning = false;
        }
    }
}
