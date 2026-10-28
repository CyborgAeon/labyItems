using labyItems.Models.Authentication;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class AccountProfileCacheTests
{
    [Fact]
    public async Task StoredProfileSurvivesANewCacheInstanceWithoutStoringPlaintextPassword()
    {
        string? stored = null;
        var cache = new AccountProfileCache(() => Task.FromResult(stored), value => { stored = value; return Task.CompletedTask; });
        var profile = new AccountProfile("Bradley", "Barfoot");
        await cache.SaveAsync("member@example.com", "secret-password", profile);
        var restarted = new AccountProfileCache(() => Task.FromResult(stored), _ => throw new InvalidOperationException("Must not rewrite cache"));
        Assert.Equal(profile, await restarted.GetAsync(" MEMBER@example.com ", "secret-password"));
        Assert.DoesNotContain("secret-password", stored);
        Assert.Null(await restarted.GetAsync("other@example.com", "secret-password"));
        Assert.Null(await restarted.GetAsync("member@example.com", "changed-password"));
    }

    [Fact]
    public async Task CorruptOrUnavailableStorageIsACacheMiss()
    {
        var corrupt = new AccountProfileCache(() => Task.FromResult<string?>("not json"), _ => Task.CompletedTask);
        Assert.Null(await corrupt.GetAsync("email", "password"));
        var unavailable = new AccountProfileCache(() => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        Assert.Null(await unavailable.GetAsync("email", "password"));
        await unavailable.SaveAsync("email", "password", new AccountProfile("Bradley", "Barfoot"));
    }
}
