using labyItems.Services;
using Microsoft.Maui.Controls.Shapes;

namespace labyItems.Pages.Calendar;

public partial class EventDetailsPage : ContentPage
{
    private readonly CalendarEventStore _eventStore;
    private readonly string _eventId;
    private CalendarEventRecord? _event;

    public EventDetailsPage(string eventId)
    {
        InitializeComponent();
        _eventId = eventId;
        _eventStore = ServiceHelper.ResolveService<CalendarEventStore>() ?? CalendarEventStore.Shared;

        BindableLayout.SetItemTemplate(PlayersSection, CreateNameTemplate());
        BindableLayout.SetItemTemplate(RefereesSection, CreateNameTemplate());
        BindableLayout.SetItemTemplate(ARefsSection, CreateNameTemplate());
        BindableLayout.SetItemTemplate(CrewSection, CreateNameTemplate());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadEvent();
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
        var result = _eventStore.TryJoinRole(_eventId, role, "admin");
        await DisplayAlert(result.Succeeded ? "Request Submitted" : "Request Blocked", result.Message, "OK");
        LoadEvent();
    }

    private void LoadEvent()
    {
        _event = _eventStore.GetEvent(_eventId);
        if (_event == null)
            return;

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

    private static DataTemplate CreateNameTemplate()
        => new(() =>
        {
            var label = new Label
            {
                FontSize = 15,
                TextColor = Colors.Black
            };
            label.SetBinding(Label.TextProperty, ".");
            return label;
        });
}
