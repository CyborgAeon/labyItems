using System.Net;
using labyItems.Models.Calendar;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class LabyrintheIntegrationTests
{
    private const string Account = "<input name='field[first_name]' value='Bradley'><input name='field[last_name]' value='Barfoot'>";
    private const string Month = """
        <div class="event-item flex ac jsb">
          <div class="ei-date"><p>31<br />Oct</p></div>
          <div class="ei-title"><p>New Player Event &amp; friends</p>
            <p class="ei-subtitle">Access: Open, Threshold: Max 250, Type: Adult players</p></div>
          <div class="ei-link"><a href="/members/event/717">Book event now</a></div>
        </div>
        """;
    private static string Bookings => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/EventBookings.html"));

    [Fact]
    public async Task LoginEncodesCredentialsAndLoadsAccountOnlyAfter302()
    {
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account));
        var session = new LabyrintheSession(new HttpClient(handler));
        Assert.True(await session.LoginAsync("a+b@example.com", "a &+=pw"));
        Assert.Equal("email=a%2Bb%40example.com&password=a+%26%2B%3Dpw&submit_login=", handler.Requests[0].Body);
        Assert.Equal("https://www.labyrinthe.co.uk/login/", handler.Requests[0].Uri);
        Assert.Equal("https://www.labyrinthe.co.uk/members/account/", handler.Requests[1].Uri);
        Assert.Equal("Bradley Barfoot", session.DisplayName);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.SeeOther)]
    public async Task Only302Authenticates(HttpStatusCode code)
    {
        var handler = new RecordingHandler(Response(code));
        var session = new LabyrintheSession(new HttpClient(handler));
        Assert.False(await session.LoginAsync("member@example.com", "secret"));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AdminSubstringFallbackMakesNoRequests()
    {
        var handler = new RecordingHandler();
        var session = new LabyrintheSession(new HttpClient(handler));
        Assert.True(await session.LoginAsync("local-admin@example.com", "admin-fallback"));
        Assert.True(session.IsFallback);
        Assert.Empty(handler.Requests);
        Assert.False(await session.PostBookingAsync("717", CalendarEventRole.Player));
    }

    [Fact]
    public void ParsesSuppliedRosterAndCapacitiesWithoutClaimingOtherMembers()
    {
        var record = Assert.Single(LabyrintheEventParser.ParseMonth(Month, new DateTime(2026, 10, 1)));
        Assert.Equal("717", record.Id);
        Assert.Equal(new DateTime(2026, 10, 31), record.Date);
        Assert.Equal("New Player Event & friends", record.Name);
        Assert.Equal("Max 250", record.Threshold);
        Assert.True(LabyrintheEventParser.PopulateDetails(record, Bookings, "Bradley Barfoot"));
        Assert.Equal(12, record.PlayerCapacity);
        Assert.Equal(8, record.Players.Count);
        Assert.Equal("Gordon Court", Assert.Single(record.Referees));
        Assert.Equal("Claudia Dauppe", Assert.Single(record.ARefs));
        Assert.Equal(4, record.CrewCapacity);
        Assert.Equal(3, record.Crew.Count);
        Assert.Null(record.MyBookedRole);
    }

    [Theory]
    [InlineData("Battle-school")]
    [InlineData("battle school")]
    public void SkipsBattleSchool(string title)
    {
        Assert.Empty(LabyrintheEventParser.ParseMonth(Month.Replace("New Player Event &amp; friends", title), new DateTime(2026, 10, 1)));
    }

    [Fact]
    public void CancellationFormIdentifiesOwnRoleAndAccountIdentifiesOwnName()
    {
        var html = Bookings.Replace("pixie meadows", "Bradley Barfoot").Replace("name=\"submit_entry\" class=\"player_entry\"", "name=\"submit_cancel\" class=\"player_entry\"");
        var record = new CalendarEventRecord();
        LabyrintheEventParser.PopulateDetails(record, html, LabyrintheEventParser.ParseAccountName(Account));
        Assert.Equal(CalendarEventRole.Player, record.MyBookedRole);
        Assert.Equal("Bradley Barfoot", record.CurrentUserName);
        Assert.Contains(record.CurrentUserName, record.Players);
    }

    [Theory]
    [InlineData(CalendarEventRole.Player, "staff_type=Player&position=Player&event_type=Adult+players&submit_entry=")]
    [InlineData(CalendarEventRole.ARef, "staff_type=A+Referee&position=A+Referee&submit_entry=")]
    [InlineData(CalendarEventRole.Crew, "staff_type=Crew&position=Crew&submit_entry=")]
    public async Task PostsBookingForms(CalendarEventRole role, string expected)
    {
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account), Response(HttpStatusCode.Found), Response(HttpStatusCode.Found));
        var session = new LabyrintheSession(new HttpClient(handler));
        await session.LoginAsync("member@example.com", "secret");
        Assert.True(await session.PostBookingAsync("717", role));
        Assert.Equal(expected, handler.Requests[2].Body);
        Assert.Equal("https://labyrinthe.co.uk/members/event/717/", handler.Requests[2].Uri);
        Assert.True(await session.PostBookingAsync("717", null));
        Assert.Equal("submit_cancel=", handler.Requests[3].Body);
        Assert.Equal("https://labyrinthe.co.uk//members/event/717/", handler.Requests[3].Uri);
    }

    [Fact]
    public async Task SessionExpiryDoesNotLookLikeSuccessfulBooking()
    {
        var redirect = Response(HttpStatusCode.Found);
        redirect.Headers.Location = new Uri("/login/", UriKind.Relative);
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account), redirect);
        var session = new LabyrintheSession(new HttpClient(handler));
        await session.LoginAsync("member@example.com", "secret");
        Assert.False(await session.PostBookingAsync("717", CalendarEventRole.Player));
    }

    [Fact]
    public async Task MonthRequestsAdvanceAndFailedBookingDoesNotChangeRoster()
    {
        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account),
            Response(HttpStatusCode.OK, Month.Replace("31<br", "1<br")), Response(HttpStatusCode.OK, ""), Response(HttpStatusCode.OK, Bookings),
            Response(HttpStatusCode.OK), Response(HttpStatusCode.OK, "<p>No events</p>"), Response(HttpStatusCode.OK, ""));
        var session = new LabyrintheSession(new HttpClient(handler));
        var store = new CalendarEventStore(session);
        await session.LoginAsync("member@example.com", "secret");
        var record = Assert.Single(await store.LoadMonthAsync(month));
        Assert.Contains($"month={month:yyyy-MM}", handler.Requests[2].Uri);
        Assert.Equal("https://www.labyrinthe.co.uk/members/bookings/", handler.Requests[3].Uri);
        Assert.Equal("https://www.labyrinthe.co.uk/members/event/717/", handler.Requests[4].Uri);
        Assert.False(await store.JoinRoleAsync(record.Id, CalendarEventRole.Player));
        Assert.DoesNotContain("Bradley Barfoot", record.Players);
        await store.LoadMonthAsync(month.AddMonths(1));
        Assert.Contains($"month={month.AddMonths(1):yyyy-MM}", handler.Requests[^2].Uri);
        var count = handler.Requests.Count;
        Assert.Empty(await store.LoadMonthAsync(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1)));
        Assert.Equal(count, handler.Requests.Count);
    }

    [Fact]
    public async Task SharesHostScopedPhpCookiesWithBookingHost()
    {
        var cookies = new CookieContainer();
        cookies.Add(new Uri("https://www.labyrinthe.co.uk/"), new Cookie("PHPSESSID", "session-value", "/"));
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account), Response(HttpStatusCode.Found));
        var session = new LabyrintheSession(new HttpClient(handler), cookies);
        await session.LoginAsync("member@example.com", "secret");
        await session.PostBookingAsync("717", CalendarEventRole.Player);
        Assert.Equal("session-value", cookies.GetCookies(new Uri("https://labyrinthe.co.uk/members/event/717/"))["PHPSESSID"]?.Value);
    }

    [Fact]
    public async Task OnlyConfirmedBookingsAndCancellationsUpdateTheRoster()
    {
        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account),
            Response(HttpStatusCode.OK, Month.Replace("31<br", "1<br")), Response(HttpStatusCode.OK, ""), Response(HttpStatusCode.OK, Bookings),
            Response(HttpStatusCode.Found), Response(HttpStatusCode.OK), Response(HttpStatusCode.Found));
        var session = new LabyrintheSession(new HttpClient(handler));
        var store = new CalendarEventStore(session);
        await session.LoginAsync("member@example.com", "secret");
        var record = Assert.Single(await store.LoadMonthAsync(month));
        Assert.True(await store.JoinRoleAsync(record.Id, CalendarEventRole.Crew));
        Assert.Contains("Bradley Barfoot", record.Crew);
        Assert.Equal(CalendarEventRole.Crew, record.MyBookedRole);
        Assert.False(await store.CancelBookingAsync(record.Id));
        Assert.Contains("Bradley Barfoot", record.Crew);
        Assert.True(await store.CancelBookingAsync(record.Id));
        Assert.DoesNotContain("Bradley Barfoot", record.Crew);
        Assert.Null(record.MyBookedRole);
    }

    [Fact]
    public async Task ReusesAccountUntilCredentialsChangeAndFailedLoginKeepsCache()
    {
        string? stored = null;
        var cache = new AccountProfileCache(() => Task.FromResult(stored), value => { stored = value; return Task.CompletedTask; });
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account),
            Response(HttpStatusCode.Found), Response(HttpStatusCode.OK),
            Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account.Replace("Bradley", "New")));
        var session = new LabyrintheSession(new HttpClient(handler)) { AccountProfiles = cache };
        Assert.False(session.CanViewCalendar);
        Assert.True(await session.LoginAsync("member@example.com", "secret"));
        Assert.True(session.CanViewCalendar);
        var original = stored;
        Assert.True(await session.LoginAsync("member@example.com", "secret"));
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(original, stored);
        Assert.False(await session.LoginAsync("different@example.com", "invalid"));
        Assert.Equal(original, stored);
        Assert.True(await session.LoginAsync("member@example.com", "new-secret"));
        Assert.Equal("New Barfoot", session.DisplayName);
        Assert.Equal(2, handler.Requests.Count(r => r.Uri.EndsWith("/members/account/")));
        Assert.True(await session.LoginAsync("admin", "admin"));
        Assert.False(session.CanViewCalendar);
    }

    private static string BookingRow(string date, string role, string id = "746") => $"""
        <div class="event-item flex ac jsb">
            <div class="ei-date"><p>{date}</p></div>
            <div class="ei-title"><p>Open Max 5250 <strong>[ Booked as {role} ]</strong></p>
            <p class="ei-subtitle">Access: Open, Threshold: Max 5250, Type: Adult players</p></div>
            <div class="ei-link"><p><a href="/members/event/{id}">View event detail »</a></p></div>
        </div>
        """;

    [Theory]
    [InlineData("Player", CalendarEventRole.Player)]
    [InlineData("Referee", CalendarEventRole.Referee)]
    [InlineData("A Referee", CalendarEventRole.ARef)]
    [InlineData("Assistant Referee", CalendarEventRole.ARef)]
    [InlineData("A-ref", CalendarEventRole.ARef)]
    [InlineData("Crew", CalendarEventRole.Crew)]
    public void ParsesSuppliedBookingRowAndRole(string label, CalendarEventRole expected)
    {
        var record = Assert.Single(LabyrintheEventParser.ParseBookings(BookingRow("31<br>Oct", label), new DateTime(2026, 10, 28)));
        Assert.Equal("746", record.Id);
        Assert.Equal("Open Max 5250", record.Name);
        Assert.Equal(new DateTime(2026, 10, 31), record.Date);
        Assert.Equal(expected, record.MyBookedRole);
        Assert.Equal("Max 5250", record.Threshold);
    }

    [Fact]
    public void BookingDatesCrossTheYearBoundaryAndRespectExplicitYears()
    {
        var today = new DateTime(2026, 12, 28);
        var nextYear = Assert.Single(LabyrintheEventParser.ParseBookings(BookingRow("2<br>Jan", "Crew"), today));
        Assert.Equal(new DateTime(2027, 1, 2), nextYear.Date);
        var thisYear = Assert.Single(LabyrintheEventParser.ParseBookings(BookingRow("31<br>Dec", "Player"), today));
        Assert.Equal(new DateTime(2026, 12, 31), thisYear.Date);
        var explicitYear = Assert.Single(LabyrintheEventParser.ParseBookings(BookingRow("2<br>Jan 2028", "Player"), today));
        Assert.Equal(new DateTime(2028, 1, 2), explicitYear.Date);
        var leapDay = Assert.Single(LabyrintheEventParser.ParseBookings(BookingRow("29<br>Feb", "Player"), today));
        Assert.Equal(new DateTime(2028, 2, 29), leapDay.Date);
    }

    [Fact]
    public void UnknownBookingRoleFailsRatherThanHighlightingTheWrongRole()
    {
        Assert.Throws<InvalidOperationException>(() => LabyrintheEventParser.ParseBookings(BookingRow("31<br>Oct", "Unknown"), new DateTime(2026, 10, 28)));
        Assert.Empty(LabyrintheEventParser.ParseBookings(BookingRow("31<br>Oct", "Player").Replace("Open Max 5250", "Battle School"), new DateTime(2026, 10, 28)));
    }

    [Fact]
    public async Task MyBookingsLoadsUnvisitedMonthsAndPreservesRoleFromTheBookingPage()
    {
        var date = DateTime.Today.AddMonths(2);
        var dateHtml = date.Day.ToString(System.Globalization.CultureInfo.InvariantCulture) + "<br>" + date.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account),
            Response(HttpStatusCode.OK, BookingRow(dateHtml, "Crew")), Response(HttpStatusCode.OK, Bookings),
            Response(HttpStatusCode.OK, "<p>No bookings</p>"));
        var session = new LabyrintheSession(new HttpClient(handler));
        var store = new CalendarEventStore(session);
        await session.LoginAsync("member@example.com", "secret");
        var record = Assert.Single(await store.LoadBookedEventsAsync());
        Assert.Equal("https://www.labyrinthe.co.uk/members/bookings/", handler.Requests[2].Uri);
        Assert.Equal("https://www.labyrinthe.co.uk/members/event/746/", handler.Requests[3].Uri);
        Assert.Equal(CalendarEventRole.Crew, record.MyBookedRole);
        Assert.Equal(3, record.Crew.Count);
        Assert.Equal(date.Date, record.Date);
        Assert.Same(record, store.GetEvent("746"));
        Assert.Empty(await store.LoadBookedEventsAsync());
        Assert.Null(store.GetEvent("746")!.MyBookedRole);
        Assert.Empty(store.GetBookedEvents());
    }

    [Fact]
    public async Task CalendarUsesBookingPageRoleEvenWhenRosterNameDoesNotMatch()
    {
        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
        var dateHtml = "1<br>" + month.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account),
            Response(HttpStatusCode.OK, Month.Replace("31<br", "1<br")),
            Response(HttpStatusCode.OK, BookingRow(dateHtml, "A Referee", "717")), Response(HttpStatusCode.OK, Bookings),
            Response(HttpStatusCode.OK, Month.Replace("31<br", "1<br")), Response(HttpStatusCode.OK, ""),
            Response(HttpStatusCode.OK, Bookings.Replace("pixie meadows", "Bradley Barfoot")));
        var session = new LabyrintheSession(new HttpClient(handler));
        var store = new CalendarEventStore(session);
        await session.LoginAsync("member@example.com", "secret");
        var record = Assert.Single(await store.LoadMonthAsync(month));
        Assert.Equal(CalendarEventRole.ARef, record.MyBookedRole);
        // A name match alone cannot claim a booking absent from the authenticated feed.
        var refreshed = Assert.Single(await store.LoadMonthAsync(month));
        Assert.Null(refreshed.MyBookedRole);
    }

    [Fact]
    public async Task BookedEventsMissingFromTheUpcomingListStillAppearInTheCalendar()
    {
        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
        var dateHtml = "1<br>" + month.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account),
            Response(HttpStatusCode.OK, ""), Response(HttpStatusCode.OK, BookingRow(dateHtml, "Player")), Response(HttpStatusCode.OK, Bookings));
        var session = new LabyrintheSession(new HttpClient(handler));
        var store = new CalendarEventStore(session);
        await session.LoginAsync("member@example.com", "secret");
        Assert.Equal(CalendarEventRole.Player, Assert.Single(await store.LoadMonthAsync(month)).MyBookedRole);
    }

    [Fact]
    public async Task FailedBookingRefreshLeavesThePreviousBookingStateIntact()
    {
        var dateHtml = "1<br>" + DateTime.Today.AddMonths(1).ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var handler = new RecordingHandler(Response(HttpStatusCode.Found), Response(HttpStatusCode.OK, Account),
            Response(HttpStatusCode.OK, BookingRow(dateHtml, "Crew")), Response(HttpStatusCode.OK, Bookings),
            Response(HttpStatusCode.InternalServerError));
        var session = new LabyrintheSession(new HttpClient(handler));
        var store = new CalendarEventStore(session);
        await session.LoginAsync("member@example.com", "secret");
        await store.LoadBookedEventsAsync();
        Assert.Equal(CalendarEventRole.Crew, Assert.Single(await store.LoadBookedEventsAsync()).MyBookedRole);
        Assert.True(store.BookingsStatus!.IsOffline);
        Assert.Equal(CalendarEventRole.Crew, Assert.Single(store.GetBookedEvents()).MyBookedRole);
    }

    private static HttpResponseMessage Response(HttpStatusCode code, string body = "") => new(code) { Content = new StringContent(body) };

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<(string Uri, string Body)> Requests { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return _responses.Dequeue();
        }
    }
}
