using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Services;
using Microsoft.Maui.Controls.Shapes;

namespace labyItems.Pages.Calendar;

public partial class CreateEventPage : ContentPage, INotifyPropertyChanged
{
    private readonly CalendarEventStore _eventStore;
    private readonly List<AreaOptionVm> _areaOptions;
    private readonly string? _editingEventId;
    private string? _selectedThreshold;
    private string? _selectedEventType;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> ThresholdOptions { get; } = new(new[]
    {
        "100", "250", "500", "750", "1000", "1500", "3000", "5250", "7500", "10000", "15000", "30000", "No-Max"
    });

    public ObservableCollection<string> EventTypeOptions { get; } = new(new[]
    {
        "Single", "Double", "Triple", "Overland", "Extended Length"
    });

    public ObservableCollection<string?> Invitees { get; } = new() { "admin@example.com" };

    public string? SelectedThreshold
    {
        get => _selectedThreshold;
        set
        {
            if (_selectedThreshold == value)
                return;

            _selectedThreshold = value;
            Raise();
        }
    }

    public string? SelectedEventType
    {
        get => _selectedEventType;
        set
        {
            if (_selectedEventType == value)
                return;

            _selectedEventType = value;
            Raise();
        }
    }

    public CreateEventPage(DateTime targetDate, string? editingEventId = null)
    {
        InitializeComponent();
        BindingContext = this;

        _eventStore = ServiceHelper.ResolveService<CalendarEventStore>() ?? CalendarEventStore.Shared;
        _editingEventId = editingEventId;
        _areaOptions =
        [
            new AreaOptionVm("A", false),
            new AreaOptionVm("B", true),
            new AreaOptionVm("C", false),
            new AreaOptionVm("D", false)
        ];

        SelectedThreshold = ThresholdOptions.FirstOrDefault();
        SelectedEventType = EventTypeOptions.FirstOrDefault();

        EventDatePicker.Date = targetDate;
        if (!string.IsNullOrWhiteSpace(_editingEventId))
            LoadExistingEvent(_editingEventId);

        BuildAreaCards();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnCreateEventClicked(object sender, EventArgs e)
    {
        var title = (TitleEntry.Text ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            await DisplayAlert("Missing title", "Enter an event title before creating the event.", "OK");
            return;
        }

        var request = new CreateCalendarEventRequest
        {
            Title = title,
            Date = EventDatePicker.Date ?? DateTime.Today,
            Location = (LocationEntry.Text ?? string.Empty).Trim(),
            Threshold = SelectedThreshold ?? "100",
            EventType = SelectedEventType ?? "Single",
            Invitees = Invitees.Where(email => !string.IsNullOrWhiteSpace(email)).Select(email => email!.Trim()).ToList(),
            BookedAreas = _areaOptions.Where(area => area.IsSelected).Select(area => area.Code).ToList()
        };

        CalendarEventRecord savedEvent;
        try
        {
            savedEvent = string.IsNullOrWhiteSpace(_editingEventId)
                ? _eventStore.AddEvent(request)
                : _eventStore.UpdateEvent(_editingEventId, request);
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlert("Unable to save", ex.Message, "OK");
            return;
        }

        await DisplayAlert(
            string.IsNullOrWhiteSpace(_editingEventId) ? "Event Created" : "Event Updated",
            string.IsNullOrWhiteSpace(_editingEventId)
                ? $"{savedEvent.Name} has been added to the calendar."
                : $"{savedEvent.Name} has been updated.",
            "OK");
        await Navigation.PopAsync();
    }

    private void LoadExistingEvent(string eventId)
    {
        var existingEvent = _eventStore.GetEvent(eventId);
        if (existingEvent == null)
            return;

        Title = "Edit Event";
        PageTitleLabel.Text = "Edit Event";
        SaveEventButton.Text = "Save Event";

        TitleEntry.Text = existingEvent.Name;
        EventDatePicker.Date = existingEvent.Date;
        LocationEntry.Text = existingEvent.Location;
        SelectedThreshold = ThresholdOptions.FirstOrDefault(option => option == existingEvent.Threshold) ?? SelectedThreshold;
        SelectedEventType = EventTypeOptions.FirstOrDefault(option => option == existingEvent.EventType) ?? SelectedEventType;

        Invitees.Clear();
        foreach (var invitee in existingEvent.Players.DefaultIfEmpty(string.Empty))
            Invitees.Add(invitee);

        foreach (var area in _areaOptions)
            area.IsSelected = existingEvent.BookedAreas.Contains(area.Code, StringComparer.OrdinalIgnoreCase);
    }

    private void BuildAreaCards()
    {
        AreaOptionsHost.Children.Clear();

        foreach (var area in _areaOptions)
        {
            var mapShape = new RoundRectangle { CornerRadius = new CornerRadius(14) };
            var mapPreview = new Border
            {
                Stroke = ResolveColor("SurfaceBorderColor", Colors.LightGray),
                StrokeShape = mapShape,
                BackgroundColor = Color.FromArgb("#F8F5EF"),
                Padding = new Thickness(12),
                Content = new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        new Label
                        {
                            Text = $"Area {area.Code}",
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 16
                        },
                        new Label
                        {
                            Text = "Map preview",
                            FontAttributes = FontAttributes.Italic,
                            TextColor = ResolveColor("Gray500", Colors.Gray)
                        },
                        new Border
                        {
                            HeightRequest = 80,
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
                            BackgroundColor = Color.FromArgb("#E8E0D0")
                        }
                    }
                }
            };

            var viewMapButton = new Button
            {
                Text = "View Map",
                BackgroundColor = Colors.White,
                BorderColor = ResolveColor("Primary", Colors.Maroon),
                BorderWidth = 1,
                TextColor = ResolveColor("Primary", Colors.Maroon)
            };
            viewMapButton.Clicked += async (_, __) => await Navigation.PushAsync(new AreaMapPage(area.Code));

            var bookingButton = new Button
            {
                Text = area.IsExternallyBooked || area.IsSelected ? "Booked" : "Book",
                IsEnabled = !(area.IsExternallyBooked || area.IsSelected)
            };
            bookingButton.Clicked += (_, __) =>
            {
                area.IsSelected = true;
                BuildAreaCards();
            };

            AreaOptionsHost.Children.Add(new Border
            {
                Padding = new Thickness(12),
                Stroke = ResolveColor("SurfaceBorderColor", Colors.LightGray),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16) },
                Content = BuildAreaCardContent(mapPreview, viewMapButton, bookingButton)
            });
        }
    }

    private static View BuildAreaCardContent(View mapPreview, Button viewMapButton, Button bookingButton)
    {
        var actionGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };

        actionGrid.Add(viewMapButton);
        actionGrid.Add(bookingButton);
        Grid.SetColumn(bookingButton, 1);

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                mapPreview,
                actionGrid
            }
        };
    }

    private static Color ResolveColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true)
        {
            if (resource is Color color)
                return color;

            if (resource is SolidColorBrush brush)
                return brush.Color;
        }

        return fallback;
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class AreaOptionVm(string code, bool isExternallyBooked)
    {
        public string Code { get; } = code;
        public bool IsExternallyBooked { get; } = isExternallyBooked;
        public bool IsSelected { get; set; }
    }
}
