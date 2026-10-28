using labyItems.Models.Calendar;
using labyItems.Services;
using labyItems.Helpers;
using Microsoft.Maui.Controls.Shapes;

namespace labyItems.Pages.Calendar;

public partial class EventDetailsPage : ContentPage
{
    private readonly CalendarEventStore _eventStore;
    private readonly string _eventId;
    private CalendarEventRecord? _event;
    private bool _isSubmitting;

    public EventDetailsPage(string eventId)
    {
        InitializeComponent();
        _eventId = eventId;
        _eventStore = ServiceHelper.ResolveService<CalendarEventStore>() ?? CalendarEventStore.Shared;

        BindableLayout.SetItemTemplate(PlayersSection, CreateNameTemplate(CalendarEventRole.Player));
        BindableLayout.SetItemTemplate(RefereesSection, CreateNameTemplate(CalendarEventRole.Referee));
        BindableLayout.SetItemTemplate(ARefsSection, CreateNameTemplate(CalendarEventRole.ARef));
        BindableLayout.SetItemTemplate(CrewSection, CreateNameTemplate(CalendarEventRole.Crew));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LoadEvent();
        await RefreshEventAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnJoinPlayerClicked(object sender, EventArgs e)
    {
        await JoinRoleAsync(CalendarEventRole.Player);
    }

    private async void OnJoinRefereeClicked(object sender, EventArgs e)
    {
        await JoinRoleAsync(CalendarEventRole.Referee);
    }

    private async void OnJoinARefClicked(object sender, EventArgs e)
    {
        await JoinRoleAsync(CalendarEventRole.ARef);
    }

    private async void OnJoinCrewClicked(object sender, EventArgs e)
    {
        await JoinRoleAsync(CalendarEventRole.Crew);
    }

    private async void OnEditEventClicked(object sender, EventArgs e)
    {
        if (_event == null)
            return;

        await Navigation.PushAsync(new CreateEventPage(_event.Date, _event.Id));
    }

    private async Task JoinRoleAsync(CalendarEventRole role)
    {
        if (_isSubmitting) return;
        _isSubmitting = true;
        IsEnabled = false;
        try
        {
            if (!await _eventStore.JoinRoleAsync(_eventId, role))
            {
                await DisplayAlert("Booking failed", "an error occurred", "OK");
                return;
            }
            LoadEvent();
            await RefreshEventAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            await DisplayAlert("Booking failed", "an error occurred", "OK");
        }
        finally { _isSubmitting = false; IsEnabled = true; }
    }

    private async Task CancelBookingAsync()
    {
        if (_isSubmitting || _event?.MyBookedRole == null) return;
        _isSubmitting = true;
        IsEnabled = false;
        try
        {
            if (!await _eventStore.CancelBookingAsync(_eventId))
            {
                await DisplayAlert("Cancellation failed", "an error occurred", "OK");
                return;
            }
            LoadEvent();
            await RefreshEventAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            await DisplayAlert("Cancellation failed", "an error occurred", "OK");
        }
        finally { _isSubmitting = false; IsEnabled = true; }
    }

    private async Task RefreshEventAsync()
    {
        try
        {
            await _eventStore.RefreshEventAsync(_eventId);
            LoadEvent();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            await DisplayAlert("Unable to refresh event", ex.Message, "OK");
        }
    }

    private void LoadEvent()
    {
        _event = _eventStore.GetEvent(_eventId);
        if (_event == null)
            return;

        BindingContext = null;
        BindingContext = _event;
        Title = _event.Name;
        PageTitleLabel.Text = _event.Name;
        EventDateLabel.Text = _event.MonthDayLabel;
        EventLocationLabel.Text = _event.Location;
        EventThresholdLabel.Text = $"{_event.EventType} / Threshold {_event.ThresholdBadge}";

        BuildProfileIcons();
        ApplySectionState();
    }

    private void BuildProfileIcons()
    {
        ProfileIconsHost.Children.Clear();
        if (_event == null)
            return;

        foreach (var _ in _event.AllParticipants.Take(8))
        {
            ProfileIconsHost.Children.Add(new Border
            {
                WidthRequest = 42,
                HeightRequest = 42,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(21) },
                Stroke = Colors.LightGray,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                Content = new Label
                {
                    Text = string.Empty,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    TextColor = Colors.Gray
                }
            });
        }
    }

    private void ApplySectionState()
    {
        if (_event == null)
            return;

        PlayersSectionCard.HeaderText = $"Players {_event.Players.Count}/{_event.PlayerCapacity}";
        RefereesSectionCard.HeaderText = $"Referees {_event.Referees.Count}/{_event.RefereeCapacity}";
        ARefsSectionCard.HeaderText = $"A-ref {_event.ARefs.Count}/{_event.ARefCapacity}";
        CrewSectionCard.HeaderText = $"Crew {_event.Crew.Count}/{_event.CrewCapacity}";
    }

    private DataTemplate CreateNameTemplate(CalendarEventRole role)
        => new(() =>
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection
                { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            var label = new Label { FontSize = 15, TextColor = Colors.Black, VerticalOptions = LayoutOptions.Center };
            label.SetBinding(Label.TextProperty, ".");
            var cancel = new Button
            {
                Text = FontAwesomeGlyphs.Trash, FontFamily = FontAwesomeGlyphs.SolidFamily,
                BackgroundColor = Colors.Transparent, TextColor = Colors.DarkRed,
                WidthRequest = 44, HeightRequest = 44, Padding = 8, IsVisible = false
            };
            SemanticProperties.SetDescription(cancel, "Cancel your booking");
            cancel.Clicked += async (_, _) => await CancelBookingAsync();
            row.BindingContextChanged += (_, _) => cancel.IsVisible = _event?.MyBookedRole == role
                && row.BindingContext is string name
                && string.Equals(name, _event.CurrentUserName, StringComparison.OrdinalIgnoreCase);
            row.Add(label, 0);
            row.Add(cancel, 1);
            return row;
        });
}
