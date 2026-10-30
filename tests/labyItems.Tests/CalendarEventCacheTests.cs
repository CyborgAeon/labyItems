using System.Net;
using labyItems.Models.Authentication;
using labyItems.Models.Calendar;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class CalendarEventCacheTests
{
    private const string Email = "member@example.com";
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private const string Account = "<input name='field[first_name]' value='Bradley'><input name='field[last_name]' value='Barfoot'>";
    private static string Key(DateTime date) => date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    private static CalendarEventRecord Event(string id, DateTime date) => new()
    {
        Id = id, Date = date, Name = "Cached event", UpdatedAtUtc = Now.AddHours(-2),
        Players = new List<string> { "Bradley Barfoot" }, MyBookedRole = CalendarEventRole.Player
    };

    [Fact]
    public async Task RestoresMonthBookingsAndDetailsFromDiskWhenOffline()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var cache = new FileCalendarEventCache(directory);
            var month = new DateTime(2026, 10, 1);
            await cache.SaveAsync(Email, new CalendarCacheSnapshot
            {
                Events = new() { Event("746", new DateTime(2026, 10, 31)) },
                MonthsUpdatedAtUtc = new() { [Key(month)] = Now.AddHours(-2) },
                BookingsUpdatedAtUtc = Now.AddHours(-2)
            });
            var session = await SessionAsync(new NetworkHandler());
            var store = new CalendarEventStore(session, new FileCalendarEventCache(directory), new FixedTime(Now));
            Assert.Equal("746", Assert.Single(await store.LoadMonthAsync(month)).Id);
            Assert.True(store.GetMonthStatus(month)!.IsOffline);
            Assert.Contains("Out of date", store.GetMonthStatus(month)!.GetMessage(Now));
            Assert.Single(await store.LoadBookedEventsAsync());
            Assert.True(store.BookingsStatus!.IsOffline);
            await store.RefreshEventAsync("746");
            Assert.True(store.GetEventStatus("746")!.IsOffline);
            Assert.Equal(Now.AddHours(-2), store.GetEventStatus("746")!.UpdatedAtUtc);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task DeletesExpiredEntriesAndHidesYesterdayWhileKeepingItsGracePeriod()
    {
        var cache = new MemoryCache();
        var month = new DateTime(2026, 10, 1);
        await cache.SaveAsync(Email, new CalendarCacheSnapshot
        {
            Events = new() { Event("old", new DateTime(2026, 10, 28)), Event("yesterday", new DateTime(2026, 10, 29)), Event("today", new DateTime(2026, 10, 30)) },
            MonthsUpdatedAtUtc = new() { [Key(month)] = Now.AddHours(-2) }
        });
        var store = new CalendarEventStore(await SessionAsync(new NetworkHandler()), cache, new FixedTime(Now));
        Assert.Equal("today", Assert.Single(await store.LoadMonthAsync(month)).Id);
        Assert.Null(store.GetEvent("yesterday"));
        var saved = await cache.LoadAsync(Email);
        Assert.DoesNotContain(saved!.Events, e => e.Id == "old");
        Assert.Contains(saved.Events, e => e.Id == "yesterday");
    }

    [Fact]
    public async Task CacheIsIsolatedByAccountAndUnseenMonthDoesNotReturnAFakeEmptyResult()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var cache = new FileCalendarEventCache(directory);
            await cache.SaveAsync(Email, new CalendarCacheSnapshot { Events = new() { Event("746", new DateTime(2026, 10, 31)) } });
            Assert.Null(await cache.LoadAsync("other@example.com"));
            var store = new CalendarEventStore(await SessionAsync(new NetworkHandler()), cache, new FixedTime(Now));
            await Assert.ThrowsAsync<HttpRequestException>(() => store.LoadMonthAsync(new DateTime(2026, 11, 1)));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void StaleFlagStartsOnlyAfterOneHour()
    {
        var status = new CalendarDataStatus(Now.AddHours(-1), false);
        Assert.DoesNotContain("Out of date", status.GetMessage(Now));
        Assert.Contains("Out of date", status.GetMessage(Now.AddSeconds(1)));
        Assert.Contains("Offline", new CalendarDataStatus(Now, true).GetMessage(Now));
    }

    [Fact]
    public async Task OfflineLoginRequiresPreviouslyVerifiedMatchingCredentialsAndReconnects()
    {
        string? saved = null;
        var profiles = new AccountProfileCache(() => Task.FromResult(saved), value => { saved = value; return Task.CompletedTask; });
        await profiles.SaveAsync(Email, "correct", new AccountProfile("Bradley", "Barfoot"));
        var handler = new OfflineThenOnlineHandler();
        var session = new LabyrintheSession(new HttpClient(handler)) { AccountProfiles = profiles };
        Assert.True(await session.LoginAsync(Email, "correct"));
        Assert.True(session.IsOfflineSession);
        Assert.True(session.CanViewCalendar);
        handler.Online = true;
        Assert.Equal("<p>Events</p>", await session.GetHtmlAsync("/members/bookings/"));
        Assert.False(session.IsOfflineSession);
        var wrong = new LabyrintheSession(new HttpClient(new NetworkHandler())) { AccountProfiles = profiles };
        await Assert.ThrowsAsync<HttpRequestException>(() => wrong.LoginAsync(Email, "incorrect"));
        Assert.False(wrong.IsAuthenticated);
    }

    [Fact]
    public async Task SuccessfulOnlineRefreshPersistsCompleteMonthAndBookingsForRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var cache = new FileCalendarEventCache(directory);
            var clock = new FixedTime(Now);
            var store = new CalendarEventStore(await SessionAsync(new EventsHandler()), cache, clock);
            var month = new DateTime(2026, 10, 1);
            Assert.Equal(CalendarEventRole.Crew, Assert.Single(await store.LoadMonthAsync(month)).MyBookedRole);
            await store.LoadBookedEventsAsync();
            var restored = new CalendarEventStore(await SessionAsync(new NetworkHandler()), new FileCalendarEventCache(directory), new FixedTime(Now.AddHours(2)));
            Assert.Single(await restored.LoadMonthAsync(month));
            Assert.Single(await restored.LoadBookedEventsAsync());
            Assert.Equal(Now, restored.GetMonthStatus(month)!.UpdatedAtUtc);
            Assert.Equal(Now, restored.BookingsStatus!.UpdatedAtUtc);
            Assert.Equal(CalendarEventRole.Crew, restored.GetEvent("746")!.MyBookedRole);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ExpiryAlsoAppliesAsTheClockAdvancesWithinTheSameSession()
    {
        var cache = new MemoryCache();
        var clock = new FixedTime(Now);
        var store = new CalendarEventStore(await SessionAsync(new EventsHandler()), cache, clock);
        await store.LoadMonthAsync(new DateTime(2026, 10, 1));
        clock.Now = Now.AddDays(3);
        Assert.Null(store.GetEvent("746"));
        await store.ExpireAsync();
        Assert.DoesNotContain((await cache.LoadAsync(Email))!.Events, e => e.Id == "746");
    }

    [Fact]
    public async Task SwitchingAccountsClearsTheInMemoryOfflineFallback()
    {
        var cache = new MemoryCache();
        var month = new DateTime(2026, 10, 1);
        await cache.SaveAsync(Email, new CalendarCacheSnapshot
        {
            Events = new() { Event("746", new DateTime(2026, 10, 31)) },
            MonthsUpdatedAtUtc = new() { [Key(month)] = Now }
        });
        var session = await SessionAsync(new NetworkHandler());
        var store = new CalendarEventStore(session, cache, new FixedTime(Now));
        Assert.Single(await store.LoadMonthAsync(month));
        await session.LoginAsync("other@example.com", "secret");
        await Assert.ThrowsAsync<HttpRequestException>(() => store.LoadMonthAsync(month));
        Assert.Null(store.GetEvent("746"));
    }

    [Fact]
    public async Task CalendarAlsoCachesUnvisitedBookingSummariesForOfflineMyBookings()
    {
        var cache = new MemoryCache();
        var online = new CalendarEventStore(await SessionAsync(new EventsHandler(includeFutureBooking: true)), cache, new FixedTime(Now));
        await online.LoadMonthAsync(new DateTime(2026, 10, 1));
        var offline = new CalendarEventStore(await SessionAsync(new NetworkHandler()), cache, new FixedTime(Now.AddHours(2)));
        Assert.Equal(2, (await offline.LoadBookedEventsAsync()).Count);
        Assert.Null(offline.GetEvent("747")!.UpdatedAtUtc); // Summary only, not a fictitious empty roster.
        await offline.RefreshEventAsync("747");
        Assert.True(offline.GetEventStatus("747")!.IsOffline);
        Assert.Equal(Now, offline.GetEventStatus("747")!.UpdatedAtUtc);
        Assert.Single(await offline.LoadMonthAsync(new DateTime(2026, 11, 1)));
        Assert.True(offline.GetMonthStatus(new DateTime(2026, 11, 1))!.IsPartial);
        Assert.Contains("other events may be missing", offline.GetMonthStatus(new DateTime(2026, 11, 1))!.GetMessage(Now));
    }

    private static async Task<LabyrintheSession> SessionAsync(HttpMessageHandler handler)
    {
        var session = new LabyrintheSession(new HttpClient(new LoginHandler(handler)));
        Assert.True(await session.LoginAsync(Email, "secret"));
        return session;
    }
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
    private sealed class MemoryCache : ICalendarEventCache
    {
        private readonly Dictionary<string, CalendarCacheSnapshot> _data = new();
        public Task<CalendarCacheSnapshot?> LoadAsync(string account) => Task.FromResult(_data.GetValueOrDefault(account));
        public Task SaveAsync(string account, CalendarCacheSnapshot snapshot) { _data[account] = snapshot; return Task.CompletedTask; }
    }
    private sealed class NetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => throw new HttpRequestException("Offline");
    }
    private sealed class LoginHandler(HttpMessageHandler handler) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _next = new(handler);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath == "/login/") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found));
            if (request.RequestUri.AbsolutePath == "/members/account/") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Account) });
            return _next.SendAsync(request, token);
        }
    }
    private sealed class EventsHandler(bool includeFutureBooking = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            string html;
            if (path.StartsWith("/members/event/", StringComparison.Ordinal))
                html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/EventBookings.html"));
            else
                html = """
                    <div class="event-item"><div class="ei-date"><p>31<br>Oct</p></div>
                    <div class="ei-title"><p>Event <strong>[ Booked as Crew ]</strong></p>
                    <p class="ei-subtitle">Threshold: Max 250, Type: Adult players</p></div>
                    <div class="ei-link"><a href="/members/event/746">View event</a></div></div>
                    """;
            if (includeFutureBooking && path == "/members/bookings/")
                html += html.Replace("746", "747").Replace("31<br>Oct", "5<br>Nov");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }

    private sealed class OfflineThenOnlineHandler : HttpMessageHandler
    {
        public bool Online { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (!Online) throw new HttpRequestException("Offline");
            return Task.FromResult(new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Found : HttpStatusCode.OK)
            { Content = new StringContent("<p>Events</p>") });
        }
    }
}
