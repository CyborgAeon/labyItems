using labyItems.Services;

namespace labyItems.Pages.Calendar;

public partial class MyBookingsPage : ContentPage
{
    private readonly CalendarEventStore _eventStore;

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
        var bookedEvents = await Task.Run(() => _eventStore.GetBookedEvents());

        CalendarVisualHelper.PopulateLegend(LegendHost);
        BookingsHost.Children.Clear();

        if (bookedEvents.Count == 0)
        {
            BookingsHost.Children.Add(new Label
            {
                Text = "You have not booked onto any events yet.",
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
}
