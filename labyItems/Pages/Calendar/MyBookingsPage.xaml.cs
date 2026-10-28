using labyItems.Services;

namespace labyItems.Pages.Calendar;

public partial class MyBookingsPage : ContentPage
{
    private readonly CalendarEventStore _eventStore;
    private bool _isLoading;

    public MyBookingsPage()
    {
        InitializeComponent();
        _eventStore = ServiceHelper.ResolveService<CalendarEventStore>() ?? CalendarEventStore.Shared;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBookingsAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async Task LoadBookingsAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        BookingsLoadingIndicator.IsVisible = BookingsLoadingIndicator.IsRunning = true;
        BookingsHost.Children.Clear();
        try
        {
            var bookedEvents = await _eventStore.LoadBookedEventsAsync();
            CalendarVisualHelper.PopulateLegend(LegendHost);
            if (bookedEvents.Count == 0)
            {
                BookingsHost.Children.Add(new Label
                {
                    Text = "You are not currently booked onto any events.",
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            await DisplayAlert("Unable to load bookings", ex.Message, "OK");
            BookingsHost.Children.Add(new Label { Text = "Bookings could not be loaded. Please try again." });
        }
        finally
        {
            _isLoading = false;
            BookingsLoadingIndicator.IsVisible = BookingsLoadingIndicator.IsRunning = false;
        }
    }
}
