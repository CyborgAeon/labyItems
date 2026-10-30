using labyItems.Models.Calendar;

namespace labyItems.Services;

/// <summary>Account-scoped snapshots, refreshed online with persistent offline fallback.</summary>
public sealed class CalendarEventStore
{
    public static CalendarEventStore Shared { get; } = new(LabyrintheSession.Shared);
    private readonly LabyrintheSession _session;
    private readonly Dictionary<string, CalendarEventRecord> _events = new();
    private readonly Dictionary<string, CalendarEventRole> _bookedRoles = new();
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly TimeProvider _time;
    private string? _loadedAccount;
    private Dictionary<string, DateTimeOffset> _monthsUpdated = new();
    private DateTimeOffset? _bookingsUpdated;
    private readonly HashSet<string> _offlineMonths = new();
    private readonly HashSet<string> _offlineEvents = new();
    private bool _bookingsOffline;
    public ICalendarEventCache? Cache { get; set; }
    private DateTime Today => _time.GetLocalNow().Date;
    private static string MonthKey(DateTime month) => month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    private static bool IsConnectionFailure(Exception ex) => ex is HttpRequestException or TaskCanceledException;

    public CalendarEventStore() : this(LabyrintheSession.Shared) { }
    public CalendarEventStore(LabyrintheSession session, ICalendarEventCache? cache = null, TimeProvider? time = null)
    {
        Cache = cache;
        _time = time ?? TimeProvider.System;
        _session = session;
        _session.SessionChanged += ResetMemory;
    }

    public async Task<IReadOnlyList<CalendarEventRecord>> LoadMonthAsync(DateTime month)
    {
        if (month.Date < new DateTime(Today.Year, Today.Month, 1))
            return Array.Empty<CalendarEventRecord>();
        await _requests.WaitAsync();
        try
        {
            await EnsureCacheLoadedAsync();
            var html = await _session.GetHtmlAsync($"/members/upcoming-events/?month={month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)}");
            var bookings = await FetchBookingsAsync();
            var roles = bookings.ToDictionary(e => e.Id, e => e.MyBookedRole);
            var events = LabyrintheEventParser.ParseMonth(html, month).Where(e => e.Date.Date >= Today).ToDictionary(e => e.Id);
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
            // The calendar also retrieves the complete bookings feed. Keep its
            // summaries for other months even before their rosters have been opened.
            foreach (var booking in bookings)
            {
                booking.CurrentUserName = _session.DisplayName;
                _events.TryAdd(booking.Id, booking);
            }
            _bookingsUpdated = _time.GetUtcNow();
            _bookingsOffline = false;
            _monthsUpdated[MonthKey(month)] = _time.GetUtcNow();
            _offlineMonths.Remove(MonthKey(month));
            await SaveCacheAsync();
            return results.OrderBy(e => e.Date).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (IsConnectionFailure(ex) && GetMonthStatus(month) != null)
        {
            _offlineMonths.Add(MonthKey(month));
            return GetEventsForMonth(month);
        }
        finally { _requests.Release(); }
    }

    public async Task<IReadOnlyList<CalendarEventRecord>> LoadBookedEventsAsync()
    {
        await _requests.WaitAsync();
        try
        {
            await EnsureCacheLoadedAsync();
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
            _bookingsUpdated = _time.GetUtcNow();
            _bookingsOffline = false;
            await SaveCacheAsync();
            return results;
        }
        catch (Exception ex) when (IsConnectionFailure(ex) && _bookingsUpdated.HasValue)
        {
            _bookingsOffline = true;
            return GetBookedEvents();
        }
        finally { _requests.Release(); }
    }

    private async Task<IReadOnlyList<CalendarEventRecord>> FetchBookingsAsync()
    {
        var html = await _session.GetHtmlAsync("/members/bookings/");
        var bookings = LabyrintheEventParser.ParseBookings(html, Today);
        // The website omits years. Do not roll a known, just-expired event into
        // next year while it remains in the site's bookings response.
        foreach (var booking in bookings)
            if (_events.TryGetValue(booking.Id, out var known)
                && known.Date.Month == booking.Date.Month && known.Date.Day == booking.Date.Day)
                booking.Date = known.Date;
        return bookings.Where(e => e.Date.Date >= Today).ToList();
    }

    private async Task<bool> PopulateEventAsync(CalendarEventRecord record, CalendarEventRole? bookedRole)
    {
        var detail = await _session.GetHtmlAsync($"/members/event/{record.Id}/");
        if (!LabyrintheEventParser.PopulateDetails(record, detail, _session.DisplayName)) return false;
        // Membership and role come from the current user's bookings page, not a
        // potentially ambiguous name match in the public roster.
        record.MyBookedRole = bookedRole;
        record.UpdatedAtUtc = _time.GetUtcNow();
        _offlineEvents.Remove(record.Id);
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
        => _events.Values.Where(e => e.Date.Date >= Today && e.Date.Year == month.Year && e.Date.Month == month.Month).OrderBy(e => e.Date).ToList();
    public IReadOnlyList<CalendarEventRecord> GetBookedEvents()
        => _events.Values.Where(e => e.Date.Date >= Today && e.MyBookedRole.HasValue).OrderBy(e => e.Date).ToList();
    public CalendarEventRecord? GetEvent(string id)
        => _events.TryGetValue(id, out var record) && record.Date.Date >= Today ? record : null;
    public bool IsDateDisabled(DateTime date) => false;
    public IReadOnlyCollection<DateTime> GetDisabledDatesForMonth(DateTime month) => Array.Empty<DateTime>();

    public async Task RefreshEventAsync(string id)
    {
        await _requests.WaitAsync();
        try
        {
            await EnsureCacheLoadedAsync();
            var record = GetEvent(id) ?? throw new InvalidOperationException("Event not found.");
            var role = _bookedRoles.TryGetValue(id, out var bookedRole) ? bookedRole : (CalendarEventRole?)null;
            if (!await PopulateEventAsync(record, role)) _events.Remove(id);
            await SaveCacheAsync();
        }
        catch (Exception ex) when (IsConnectionFailure(ex) && GetEventStatus(id) != null)
        {
            _offlineEvents.Add(id);
        }
        finally { _requests.Release(); }
    }

    public async Task<bool> JoinRoleAsync(string id, CalendarEventRole role)
    {
        await _requests.WaitAsync();
        try
        {
            await EnsureCacheLoadedAsync();
            if (GetEvent(id) == null || !await _session.PostBookingAsync(id, role)) return false;
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
            await SaveCacheAsync();
            return true;
        }
        finally { _requests.Release(); }
    }

    public async Task<bool> CancelBookingAsync(string id)
    {
        await _requests.WaitAsync();
        try
        {
            await EnsureCacheLoadedAsync();
            var record = GetEvent(id);
            if (record == null || !record.MyBookedRole.HasValue) return false;
            if (!await _session.PostBookingAsync(id, null)) return false;
            RemoveCurrentUser(record);
            record.MyBookedRole = null;
            _bookedRoles.Remove(id);
            await SaveCacheAsync();
            return true;
        }
        finally { _requests.Release(); }
    }

    public CalendarDataStatus? GetMonthStatus(DateTime month)
    {
        var offline = _offlineMonths.Contains(MonthKey(month));
        if (_monthsUpdated.TryGetValue(MonthKey(month), out var updated))
            return new CalendarDataStatus(updated, offline);
        var saved = GetEventsForMonth(month);
        return saved.Count > 0 && _bookingsUpdated is { } bookingsUpdated
            ? new CalendarDataStatus(saved.Select(e => e.UpdatedAtUtc ?? bookingsUpdated).Append(bookingsUpdated).Min(), offline, IsPartial: true)
            : null;
    }
    public CalendarDataStatus? BookingsStatus
        => _bookingsUpdated is { } updated
            ? new CalendarDataStatus(GetBookedEvents().Select(e => e.UpdatedAtUtc ?? updated).Append(updated).Min(), _bookingsOffline) : null;
    public CalendarDataStatus? GetEventStatus(string id)
        => (GetEvent(id)?.UpdatedAtUtc ?? (GetEvent(id)?.MyBookedRole != null ? _bookingsUpdated : null)) is { } updated
            ? new CalendarDataStatus(updated, _offlineEvents.Contains(id)) : null;

    private void ResetMemory()
    {
        _events.Clear();
        _bookedRoles.Clear();
        _monthsUpdated.Clear();
        _bookingsUpdated = null;
        _offlineMonths.Clear();
        _offlineEvents.Clear();
        _bookingsOffline = false;
        _loadedAccount = null;
    }

    public async Task ExpireAsync()
    {
        await _requests.WaitAsync();
        try { await EnsureCacheLoadedAsync(); }
        finally { _requests.Release(); }
    }

    private async Task EnsureCacheLoadedAsync()
    {
        if (!_session.CanViewCalendar) throw new InvalidOperationException("Sign in with a member account to view events.");
        var account = _session.Email.Trim().ToLowerInvariant();
        if (_loadedAccount != account)
        {
            ResetMemory();
            _loadedAccount = account;
            var snapshot = Cache == null ? null : await Cache.LoadAsync(account);
            if (snapshot != null)
            {
                foreach (var record in snapshot.Events) _events[record.Id] = record;
                _monthsUpdated = snapshot.MonthsUpdatedAtUtc;
                _bookingsUpdated = snapshot.BookingsUpdatedAtUtc;
                foreach (var record in _events.Values)
                    if (record.MyBookedRole is { } role) _bookedRoles[record.Id] = role;
            }
        }
        await SaveCacheAsync(); // Apply expiry on startup and every subsequent access.
    }

    private async Task SaveCacheAsync()
    {
        foreach (var id in _events.Values.Where(e => e.Date.Date < Today.AddDays(-1)).Select(e => e.Id).ToList())
        {
            _events.Remove(id);
            _bookedRoles.Remove(id);
            _offlineEvents.Remove(id);
        }
        foreach (var month in _monthsUpdated.Keys.Where(m => string.CompareOrdinal(m, MonthKey(Today)) < 0).ToList())
            _monthsUpdated.Remove(month);
        if (Cache == null || _loadedAccount == null) return;
        await Cache.SaveAsync(_loadedAccount, new CalendarCacheSnapshot
        {
            Events = _events.Values.ToList(),
            MonthsUpdatedAtUtc = new Dictionary<string, DateTimeOffset>(_monthsUpdated),
            BookingsUpdatedAtUtc = _bookingsUpdated
        });
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
