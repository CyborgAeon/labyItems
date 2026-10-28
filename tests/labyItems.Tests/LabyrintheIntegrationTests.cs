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
            Response(HttpStatusCode.OK, Month.Replace("31<br", "1<br")), Response(HttpStatusCode.OK, Bookings),
            Response(HttpStatusCode.OK), Response(HttpStatusCode.OK, "<p>No events</p>"));
        var session = new LabyrintheSession(new HttpClient(handler));
        var store = new CalendarEventStore(session);
        await session.LoginAsync("member@example.com", "secret");
        var record = Assert.Single(await store.LoadMonthAsync(month));
        Assert.Contains($"month={month:yyyy-MM}", handler.Requests[2].Uri);
        Assert.Equal("https://www.labyrinthe.co.uk/members/event/717", handler.Requests[3].Uri);
        Assert.False(await store.JoinRoleAsync(record.Id, CalendarEventRole.Player));
        Assert.DoesNotContain("Bradley Barfoot", record.Players);
        await store.LoadMonthAsync(month.AddMonths(1));
        Assert.Contains($"month={month.AddMonths(1):yyyy-MM}", handler.Requests[^1].Uri);
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
            Response(HttpStatusCode.OK, Month.Replace("31<br", "1<br")), Response(HttpStatusCode.OK, Bookings),
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
