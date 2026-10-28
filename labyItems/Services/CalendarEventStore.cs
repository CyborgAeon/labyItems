using labyItems.Models.Calendar;

namespace labyItems.Services;

/// <summary>Transient snapshots for the visible calendar; nothing is written to app storage.</summary>
public sealed class CalendarEventStore
{
    public static CalendarEventStore Shared { get; } = new(LabyrintheSession.Shared);
    private readonly LabyrintheSession _session;
    private readonly Dictionary<string, CalendarEventRecord> _events = new();
    private readonly Dictionary<string, CalendarEventRole> _bookedRoles = new();
    private readonly SemaphoreSlim _requests = new(1, 1);

    public CalendarEventStore() : this(LabyrintheSession.Shared) { }
    public CalendarEventStore(LabyrintheSession session)
    {
        _session = session;
        _session.SessionChanged += () =>
        {
            _events.Clear();
            _bookedRoles.Clear();
        };
    }

    public async Task<IReadOnlyList<CalendarEventRecord>> LoadMonthAsync(DateTime month)
    {
        if (month.Date < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1))
            return Array.Empty<CalendarEventRecord>();
        await _requests.WaitAsync();
        try
        {
            var html = await _session.GetHtmlAsync($"/members/upcoming-events/?month={month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)}");
            var bookings = await FetchBookingsAsync();
            var roles = bookings.ToDictionary(e => e.Id, e => e.MyBookedRole);
            var events = LabyrintheEventParser.ParseMonth(html, month).ToDictionary(e => e.Id);
            // Include booked events even if the upcoming-events page omits full events.
            foreach (var booking in bookings.Where(e => e.Date.Year == month.Year && e.Date.Month == month.Month))
                events.TryAdd(booking.Id, booking);
            var results = new List<CalendarEventRecord>();
            foreach (var record in events.Values)
            {
                if (await PopulateEventAsync(record, roles.GetValueOrDefault(record.Id)))
                    results.Add(record);
            }
            ReplaceBookedRoles(bookings);
            foreach (var id in _events.Values.Where(e => e.Date.Year == month.Year && e.Date.Month == month.Month).Select(e => e.Id).ToList())
                _events.Remove(id);
            foreach (var record in results) _events[record.Id] = record;
            return results.OrderBy(e => e.Date).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        finally { _requests.Release(); }
    }

    public async Task<IReadOnlyList<CalendarEventRecord>> LoadBookedEventsAsync()
    {
        await _requests.WaitAsync();
        try
        {
            var bookings = await FetchBookingsAsync();
            var results = new List<CalendarEventRecord>();
            foreach (var record in bookings)
            {
                if (await PopulateEventAsync(record, record.MyBookedRole)) results.Add(record);
            }
            // Publish only a complete successful response, so a failed request cannot
            // clear existing booking state or leave a partially loaded list.
            ReplaceBookedRoles(results);
            foreach (var record in results) _events[record.Id] = record;
            return results;
        }
        finally { _requests.Release(); }
    }

    private async Task<IReadOnlyList<CalendarEventRecord>> FetchBookingsAsync()
    {
        var html = await _session.GetHtmlAsync("/members/bookings/");
        return LabyrintheEventParser.ParseBookings(html, DateTime.Today);
    }

    private async Task<bool> PopulateEventAsync(CalendarEventRecord record, CalendarEventRole? bookedRole)
    {
        var detail = await _session.GetHtmlAsync($"/members/event/{record.Id}/");
        if (!LabyrintheEventParser.PopulateDetails(record, detail, _session.DisplayName)) return false;
        // Membership and role come from the current user's bookings page, not a
        // potentially ambiguous name match in the public roster.
        record.MyBookedRole = bookedRole;
        return true;
    }

    private void ReplaceBookedRoles(IEnumerable<CalendarEventRecord> bookings)
    {
        _bookedRoles.Clear();
        foreach (var record in bookings)
            if (record.MyBookedRole is { } role) _bookedRoles[record.Id] = role;
        foreach (var record in _events.Values)
            record.MyBookedRole = _bookedRoles.TryGetValue(record.Id, out var role) ? role : null;
    }

    public IReadOnlyList<CalendarEventRecord> GetEventsForMonth(DateTime month)
        => _events.Values.Where(e => e.Date.Year == month.Year && e.Date.Month == month.Month).OrderBy(e => e.Date).ToList();
    public IReadOnlyList<CalendarEventRecord> GetBookedEvents()
        => _events.Values.Where(e => e.MyBookedRole.HasValue).OrderBy(e => e.Date).ToList();
    public CalendarEventRecord? GetEvent(string id) => _events.GetValueOrDefault(id);
    public bool IsDateDisabled(DateTime date) => false;
    public IReadOnlyCollection<DateTime> GetDisabledDatesForMonth(DateTime month) => Array.Empty<DateTime>();

    public async Task RefreshEventAsync(string id)
    {
        await _requests.WaitAsync();
        try
        {
            if (!_events.TryGetValue(id, out var record)) throw new InvalidOperationException("Event not found.");
            var role = _bookedRoles.TryGetValue(id, out var bookedRole) ? bookedRole : (CalendarEventRole?)null;
            if (!await PopulateEventAsync(record, role)) _events.Remove(id);
        }
        finally { _requests.Release(); }
    }

    public async Task<bool> JoinRoleAsync(string id, CalendarEventRole role)
    {
        await _requests.WaitAsync();
        try
        {
            if (!await _session.PostBookingAsync(id, role)) return false;
            _bookedRoles[id] = role;
            if (_events.TryGetValue(id, out var record))
            {
                RemoveCurrentUser(record);
                var names = role switch
                {
                    CalendarEventRole.Player => record.Players, CalendarEventRole.ARef => record.ARefs,
                    CalendarEventRole.Crew => record.Crew, _ => record.Referees
                };
                names.Add(_session.DisplayName);
                record.MyBookedRole = role;
            }
            return true;
        }
        finally { _requests.Release(); }
    }

    public async Task<bool> CancelBookingAsync(string id)
    {
        await _requests.WaitAsync();
        try
        {
            if (!_events.TryGetValue(id, out var record) || !record.MyBookedRole.HasValue) return false;
            if (!await _session.PostBookingAsync(id, null)) return false;
            RemoveCurrentUser(record);
            record.MyBookedRole = null;
            _bookedRoles.Remove(id);
            return true;
        }
        finally { _requests.Release(); }
    }

    private void RemoveCurrentUser(CalendarEventRecord record)
    {
        foreach (var names in new[] { record.Players, record.Referees, record.ARefs, record.Crew })
            names.RemoveAll(n => string.Equals(n, _session.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

    // Server-side creation/editing has no supplied contract; never fabricate live events.
    public CalendarEventRecord AddEvent(CreateCalendarEventRequest request)
        => throw new InvalidOperationException("Create events on the Labyrinthe website.");
    public CalendarEventRecord UpdateEvent(string id, CreateCalendarEventRequest request)
        => throw new InvalidOperationException("Edit events on the Labyrinthe website.");
}
