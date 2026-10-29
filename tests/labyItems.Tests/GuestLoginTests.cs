using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class GuestLoginTests
{
    [Fact]
    public async Task GuestCanEnterAppButCannotAccessCalendarOrBookings()
    {
        var session = new LabyrintheSession();
        session.LoginAsGuest();

        Assert.True(session.IsAuthenticated);
        Assert.True(session.IsFallback);
        Assert.Equal("Guest", session.DisplayName);
        Assert.Empty(session.Email);
        Assert.Null(session.Account);
        Assert.False(session.CanViewCalendar);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.GetHtmlAsync("/members/events/"));
        Assert.False(await session.PostBookingAsync("123", null));
    }
}
