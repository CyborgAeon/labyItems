using System.Net;
using labyItems.Models.Authentication;
using labyItems.Models.Calendar;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class OfflineFirstLoginTests
{
    [Fact]
    public async Task DifferentPasswordMustWaitForServerAndCannotUnlockFromTheLocalProfile()
    {
        var handler = new PendingLoginHandler();
        var session = await CreateSession(handler);
        var login = session.LoginAsync("member@example.com", "wrong");
        Assert.False(login.IsCompleted);
        Assert.False(session.IsAuthenticated);
        handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
        Assert.False(await login);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task VerifiedCredentialsUnlockBeforeServerRespondsAndKeepWorkspaceVersionWhenConnected()
    {
        var handler = new PendingLoginHandler();
        var session = await CreateSession(handler);
        Assert.True(await session.LoginAsync("member@example.com", "correct").WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(session.IsOfflineSession);
        var version = session.Version;
        handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.Found));
        Assert.Equal("events", await session.GetHtmlAsync("/members/bookings/"));
        Assert.False(session.IsOfflineSession);
        Assert.Equal(version, session.Version);
        Assert.Equal(1, handler.LoginRequests);
    }

    [Fact]
    public async Task LateAuthenticationCannotUnlockTheNextAccount()
    {
        var handler = new PendingLoginHandler();
        var session = await CreateSession(handler);
        Assert.True(await session.LoginAsync("member@example.com", "correct"));
        session.LoginAsGuest();
        handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.Found));
        await handler.Responded.Task;
        Assert.True(session.IsFallback);
        Assert.Null(session.Account);
        Assert.Equal("Guest", session.DisplayName);
    }

    [Fact]
    public async Task CalendarAndBookingsReturnDiskSnapshotWhileAuthenticationIsPending()
    {
        var handler = new PendingLoginHandler();
        var session = await CreateSession(handler);
        Assert.True(await session.LoginAsync("member@example.com", "correct"));
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var cache = new FileCalendarEventCache(path);
            var date = DateTime.Today.AddDays(1);
            await cache.SaveAsync(session.Email, new CalendarCacheSnapshot
            {
                Events = new() { new CalendarEventRecord { Id = "746", Name = "Saved event", Date = date, MyBookedRole = CalendarEventRole.Crew } },
                MonthsUpdatedAtUtc = new() { [date.ToString("yyyy-MM")] = DateTimeOffset.UtcNow.AddHours(-2) },
                BookingsUpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-2)
            });
            var store = new CalendarEventStore(session, cache);
            Assert.Equal("746", Assert.Single(await store.ReadMonthAndRefreshAsync(date).WaitAsync(TimeSpan.FromSeconds(2))).Id);
            Assert.Single(await store.ReadBookingsAndRefreshAsync().WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.False(handler.Response.Task.IsCompleted);
            session.Logout();
            handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.Found));
        }
        finally { Directory.Delete(path, true); }
    }

    private static async Task<LabyrintheSession> CreateSession(PendingLoginHandler handler)
    {
        string? json = null;
        var cache = new AccountProfileCache(() => Task.FromResult(json), value => { json = value; return Task.CompletedTask; });
        await cache.SaveAsync("member@example.com", "correct", new AccountProfile("Bradley", "Barfoot"));
        return new LabyrintheSession(new HttpClient(handler)) { AccountProfiles = cache };
    }

    private sealed class PendingLoginHandler : HttpMessageHandler
    {
        public TaskCompletionSource<HttpResponseMessage> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Responded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int LoginRequests;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Post) return new(HttpStatusCode.OK) { Content = new StringContent("events") };
            Interlocked.Increment(ref LoginRequests);
            var response = await Response.Task;
            Responded.TrySetResult();
            return response;
        }
    }
}
