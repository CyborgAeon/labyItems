using labyItems.Models.Calendar;

namespace labyItems.Services;

/// <summary>Transient snapshots for the visible calendar; nothing is written to app storage.</summary>
public sealed class CalendarEventStore
{
    public static CalendarEventStore Shared { get; } = new(LabyrintheSession.Shared);
    private readonly LabyrintheSession _session;
    private readonly Dictionary<string, CalendarEventRecord> _events = new();
    private readonly SemaphoreSlim _requests = new(1, 1);

    public CalendarEventStore() : this(LabyrintheSession.Shared) { }
    public CalendarEventStore(LabyrintheSession session)
    {
        _session = session;
        _session.SessionChanged += () => _events.Clear();
    }

    public async Task<IReadOnlyList<CalendarEventRecord>> LoadMonthAsync(DateTime month)
    {
        if (month.Date < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1))
            return Array.Empty<CalendarEventRecord>();
        await _requests.WaitAsync();
        try
        {
            var html = await _session.GetHtmlAsync($"/members/upcoming-events/?month={month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)}");
            var events = LabyrintheEventParser.ParseMonth(html, month);
            var results = new List<CalendarEventRecord>();
            // Sequential requests avoid flooding the member website with detail requests.
            foreach (var record in events)
            {
                var detail = await _session.GetHtmlAsync($"/members/event/{record.Id}/");
                if (LabyrintheEventParser.PopulateDetails(record, detail, _session.DisplayName)) results.Add(record);
            }
            foreach (var id in _events.Values.Where(e => e.Date.Year == month.Year && e.Date.Month == month.Month).Select(e => e.Id).ToList())
                _events.Remove(id);
            foreach (var record in results) _events[record.Id] = record;
            return results;
        }
        finally { _requests.Release(); }
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
            var html = await _session.GetHtmlAsync($"/members/event/{id}/");
            if (!LabyrintheEventParser.PopulateDetails(record, html, _session.DisplayName)) _events.Remove(id);
        }
        finally { _requests.Release(); }
    }

    public async Task<bool> JoinRoleAsync(string id, CalendarEventRole role)
    {
        if (!await _session.PostBookingAsync(id, role)) return false;
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
    public async Task<bool> CancelBookingAsync(string id)
    {
        if (!_events.TryGetValue(id, out var record) || !record.MyBookedRole.HasValue) return false;
        if (!await _session.PostBookingAsync(id, null)) return false;
        RemoveCurrentUser(record);
        record.MyBookedRole = null;
        return true;
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
