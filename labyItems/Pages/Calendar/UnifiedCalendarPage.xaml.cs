using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace labyItems.Pages.Calendar;

public partial class UnifiedCalendarPage : ContentPage, INotifyPropertyChanged
{
    private readonly CalendarEventStore _eventStore;
    private bool _isAnimatingCalendarCollapse;
    private bool _isAnimatingMonthTransition;
    private bool _isPerformingPageFade;
    private DateTime _visibleMonth;
    private DateTime? _selectedDay;
    private bool _isCalendarExpanded = true;
    public ICommand BackNavigationCommand { get; }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CalendarEventRecord> VisibleEvents { get; } = new();

    public UnifiedCalendarPage()
    {
        BackNavigationCommand = new Command(async () => await NavigateBackAsync());
        InitializeComponent();
        BindingContext = this;
        _eventStore = ServiceHelper.ResolveService<CalendarEventStore>() ?? CalendarEventStore.Shared;
        _visibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        MonthButton.Text = _visibleMonth.ToString("MMMM");
        YearButton.Text = _visibleMonth.ToString("yyyy");
        PageContent.Opacity = 0;
        ToggleCalendarButton.Rotation = 0;
        _ = RefreshMonthAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshMonthAsync();
        if (!_isPerformingPageFade)
        {
            PageContent.Opacity = 0;
            await PageContent.FadeTo(1, 140, Easing.CubicOut);
        }
        else
        {
            _isPerformingPageFade = false;
            PageContent.Opacity = 1;
        }
    }

    private async Task NavigateBackAsync()
    {
        if (Navigation?.NavigationStack?.Count > 1)
        {
            await NavigateWithFadeAsync(() => Navigation.PopAsync());
            return;
        }

        if (Shell.Current != null)
            await NavigateWithFadeAsync(() => Shell.Current.GoToAsync(".."));
    }

    private async void OnMonthTitleClicked(object sender, EventArgs e)
    {
        var monthNames = Enumerable.Range(1, 12)
            .Select(month => new DateTime(2000, month, 1).ToString("MMMM"))
            .ToArray();

        var selection = await DisplayActionSheet("Select month", "Cancel", null, monthNames);
        if (string.IsNullOrWhiteSpace(selection) || string.Equals(selection, "Cancel", StringComparison.Ordinal))
            return;

        var monthIndex = Array.FindIndex(monthNames, month => string.Equals(month, selection, StringComparison.Ordinal));
        if (monthIndex < 0)
            return;

        var targetMonth = new DateTime(_visibleMonth.Year, monthIndex + 1, 1);
        var direction = targetMonth > _visibleMonth ? 1 : -1;
        await ChangeVisibleMonthAsync(targetMonth, direction == 0 ? 1 : direction);
    }

    private async void OnYearTitleClicked(object sender, EventArgs e)
    {
        var startYear = DateTime.Today.Year - 3;
        var yearOptions = Enumerable.Range(startYear, 7)
            .Select(year => year.ToString())
            .ToArray();

        var selection = await DisplayActionSheet("Select year", "Cancel", null, yearOptions);
        if (string.IsNullOrWhiteSpace(selection) || string.Equals(selection, "Cancel", StringComparison.Ordinal))
            return;

        if (!int.TryParse(selection, out var year))
            return;

        var targetMonth = new DateTime(year, _visibleMonth.Month, 1);
        var direction = targetMonth > _visibleMonth ? 1 : -1;
        await ChangeVisibleMonthAsync(targetMonth, direction == 0 ? 1 : direction);
    }

    private async void OnToggleCalendarClicked(object sender, EventArgs e)
    {
        if (_isAnimatingCalendarCollapse)
            return;

        _isAnimatingCalendarCollapse = true;
        _isCalendarExpanded = !_isCalendarExpanded;
        CalendarBodyHost.IsExpanded = _isCalendarExpanded;

        try
        {
            await ToggleCalendarButton.RotateTo(_isCalendarExpanded ? 0 : 180, 140, Easing.CubicOut);
        }
        finally
        {
            _isAnimatingCalendarCollapse = false;
        }
    }

    private async void OnNextMonthSwiped(object sender, SwipedEventArgs e)
    {
        await ChangeVisibleMonthAsync(_visibleMonth.AddMonths(1), 1);
    }

    private async void OnPreviousMonthSwiped(object sender, SwipedEventArgs e)
    {
        await ChangeVisibleMonthAsync(_visibleMonth.AddMonths(-1), -1);
    }

    private async void OnCreateEventClicked(object sender, EventArgs e)
    {
        var targetDate = _selectedDay
            ?? (_visibleMonth.Month == DateTime.Today.Month && _visibleMonth.Year == DateTime.Today.Year
                ? DateTime.Today
                : _visibleMonth);

        if (_eventStore.IsDateDisabled(targetDate))
        {
            await DisplayAlert("Date unavailable", "That weekend is currently unavailable for booking.", "OK");
            return;
        }

        await NavigateWithFadeAsync(() => Navigation.PushAsync(new CreateEventPage(targetDate)));
    }

    private async void OnOpenMyBookingsClicked(object sender, EventArgs e)
    {
        await NavigateWithFadeAsync(() => Navigation.PushAsync(new MyBookingsPage()));
    }

    private async Task OpenEventAsync(CalendarEventRecord selectedEvent)
    {
        await NavigateWithFadeAsync(() => Navigation.PushAsync(new EventDetailsPage(selectedEvent.Id)));
    }

    private async Task RefreshMonthAsync()
    {
        var monthEvents = await Task.Run(() => _eventStore.GetEventsForMonth(_visibleMonth));
        RefreshMonthContent(monthEvents);
        CalendarBodyHost.IsExpanded = _isCalendarExpanded;
        ToggleCalendarButton.Rotation = _isCalendarExpanded ? 0 : 180;
        Raise(nameof(VisibleEvents));
    }

    private void RefreshMonthContent(IReadOnlyList<CalendarEventRecord> monthEvents)
    {
        var visible = monthEvents
            .Where(evt => !_selectedDay.HasValue || evt.Date.Date == _selectedDay.Value.Date)
            .ToList();

        VisibleEvents.Clear();
        foreach (var evt in visible)
            VisibleEvents.Add(evt);

        MonthButton.Text = _visibleMonth.ToString("MMMM");
        YearButton.Text = _visibleMonth.ToString("yyyy");
        SelectedDayLabel.Text = _selectedDay.HasValue
            ? $"Filtering by {_selectedDay.Value:dddd d MMMM}. Tap the same day again to clear."
            : "Tap a day to filter the event list below. Swipe left or right to change month.";

        BuildCalendar(monthEvents);
        CalendarVisualHelper.PopulateLegend(LegendHost);
        BuildEvents();
    }

    private async Task ChangeVisibleMonthAsync(DateTime targetMonth, int direction)
    {
        if (_isAnimatingMonthTransition || targetMonth == _visibleMonth)
            return;

        _isAnimatingMonthTransition = true;

        try
        {
            var outgoingOffset = direction > 0 ? -48 : 48;
            var incomingOffset = -outgoingOffset;

            await Task.WhenAll(
                CalendarTitleHost.TranslateTo(outgoingOffset, 0, 110, Easing.CubicIn),
                CalendarTitleHost.FadeTo(0, 110, Easing.CubicIn),
                CalendarHost.TranslateTo(outgoingOffset, 0, 110, Easing.CubicIn),
                CalendarHost.FadeTo(0, 110, Easing.CubicIn),
                EventsHost.FadeTo(0, 90, Easing.CubicIn));

            _visibleMonth = targetMonth;
            _selectedDay = null;

            var monthEvents = await Task.Run(() => _eventStore.GetEventsForMonth(_visibleMonth));
            RefreshMonthContent(monthEvents);

            CalendarTitleHost.TranslationX = incomingOffset;
            CalendarTitleHost.Opacity = 0;
            CalendarHost.TranslationX = incomingOffset;
            CalendarHost.Opacity = 0;
            EventsHost.Opacity = 0;

            await Task.WhenAll(
                CalendarTitleHost.TranslateTo(0, 0, 150, Easing.CubicOut),
                CalendarTitleHost.FadeTo(1, 150, Easing.CubicOut),
                CalendarHost.TranslateTo(0, 0, 150, Easing.CubicOut),
                CalendarHost.FadeTo(1, 150, Easing.CubicOut),
                EventsHost.FadeTo(1, 120, Easing.CubicOut));

            Raise(nameof(VisibleEvents));
        }
        finally
        {
            _isAnimatingMonthTransition = false;
        }
    }

    private async Task RefreshVisibleEventsAsync()
    {
        await EventsHost.FadeTo(0, 70, Easing.CubicIn);

        var monthEvents = await Task.Run(() => _eventStore.GetEventsForMonth(_visibleMonth));
        RefreshMonthContent(monthEvents);

        EventsHost.Opacity = 0;
        await EventsHost.FadeTo(1, 110, Easing.CubicOut);
        Raise(nameof(VisibleEvents));
    }

    private void BuildCalendar(IReadOnlyList<CalendarEventRecord> monthEvents)
    {
        CalendarHost.Children.Clear();

        var headings = new Grid
        {
            ColumnDefinitions = BuildWeekColumns(),
            ColumnSpacing = 6
        };

        var dayNames = new[] { "M", "T", "W", "T", "F", "S", "S" };
        for (var i = 0; i < dayNames.Length; i++)
        {
            headings.Add(new Label
            {
                Text = dayNames[i],
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = Colors.Black
            }, i, 0);
        }

        CalendarHost.Children.Add(headings);

        var grid = new Grid
        {
            ColumnDefinitions = BuildWeekColumns(),
            ColumnSpacing = 6,
            RowSpacing = 6
        };

        var firstOfMonth = new DateTime(_visibleMonth.Year, _visibleMonth.Month, 1);
        var firstColumn = ((int)firstOfMonth.DayOfWeek + 6) % 7;
        var daysInMonth = DateTime.DaysInMonth(_visibleMonth.Year, _visibleMonth.Month);
        var eventDays = monthEvents.GroupBy(evt => evt.Date.Day).ToDictionary(group => group.Key, group => group.ToList());

        var row = 0;
        var column = firstColumn;
        for (var day = 1; day <= daysInMonth; day++)
        {
            if (column == 7)
            {
                column = 0;
                row++;
            }

            if (grid.RowDefinitions.Count <= row)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var date = new DateTime(_visibleMonth.Year, _visibleMonth.Month, day);
            var isSelected = _selectedDay.HasValue && _selectedDay.Value.Date == date.Date;
            var isDisabled = _eventStore.IsDateDisabled(date);
            var hasEvents = eventDays.TryGetValue(day, out var dayEvents);
            var bookedRole = dayEvents?
                .Select(evt => evt.MyBookedRole)
                .FirstOrDefault(role => role.HasValue);

            var dayLabel = new Label
            {
                Text = day.ToString(),
                FontSize = 13,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = CalendarVisualHelper.ResolveDayTextColor(isSelected, bookedRole, isDisabled)
            };

            var dayBorder = new Border
            {
                Padding = new Thickness(6, 8),
                StrokeThickness = 0,
                MinimumHeightRequest = 58,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
                BackgroundColor = CalendarVisualHelper.ResolveDayBackgroundColor(isSelected, hasEvents, bookedRole, isDisabled),
                Content = dayLabel
            };

            if (!isDisabled)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, __) =>
                {
                    _selectedDay = _selectedDay.HasValue && _selectedDay.Value.Date == date.Date
                        ? null
                        : date;

                    await RefreshVisibleEventsAsync();
                };
                dayBorder.GestureRecognizers.Add(tap);

                var nextSwipe = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
                nextSwipe.Swiped += OnNextMonthSwiped;
                dayBorder.GestureRecognizers.Add(nextSwipe);

                var previousSwipe = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
                previousSwipe.Swiped += OnPreviousMonthSwiped;
                dayBorder.GestureRecognizers.Add(previousSwipe);
            }

            grid.Add(dayBorder, column, row);
            column++;
        }

        CalendarHost.Children.Add(grid);
    }

    private void BuildEvents()
    {
        EventsHost.Children.Clear();

        if (VisibleEvents.Count == 0)
        {
            EventsHost.Children.Add(new Label
            {
                Text = "No events match this filter.",
                FontAttributes = FontAttributes.Italic,
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var evt in VisibleEvents)
            EventsHost.Children.Add(CalendarVisualHelper.BuildEventCard(evt, () => OpenEventAsync(evt)));
    }

    private static ColumnDefinitionCollection BuildWeekColumns()
    {
        var columns = new ColumnDefinitionCollection();
        for (var index = 0; index < 7; index++)
            columns.Add(new ColumnDefinition { Width = GridLength.Star });

        return columns;
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async Task NavigateWithFadeAsync(Func<Task> navigationAction)
    {
        if (_isPerformingPageFade)
            return;

        _isPerformingPageFade = true;

        try
        {
            await PageContent.FadeTo(0, 110, Easing.CubicIn);
            await navigationAction();
        }
        catch
        {
            PageContent.Opacity = 0;
            await PageContent.FadeTo(1, 110, Easing.CubicOut);
            _isPerformingPageFade = false;
            throw;
        }
    }
}
