using labyItems.Models.Authentication;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class SavedLoginStoreTests
{
    [Fact]
    public async Task CancelledOrFailedBiometricsNeverReadsCredentials()
    {
        var storage = new FakeStorage { Login = new SavedLogin("member@example.com", "secret") };
        var store = new SavedLoginStore(storage, () => Task.FromResult(false));
        Assert.Null(await store.UnlockAsync());
        Assert.Equal(0, storage.ReadCount);
        Assert.True(store.HasSavedLogin);
    }

    [Fact]
    public async Task SuccessfulBiometricsAllowsReadingSavedCredentials()
    {
        var storage = new FakeStorage { Login = new SavedLogin("member@example.com", "secret") };
        var store = new SavedLoginStore(storage, () => Task.FromResult(true));
        Assert.Equal(storage.Login, await store.UnlockAsync());
        Assert.Equal(1, storage.ReadCount);
        store.Remove();
        Assert.False(store.HasSavedLogin);
    }

    [Fact]
    public async Task NoSavedLoginDoesNotPromptForBiometrics()
    {
        var storage = new FakeStorage();
        var store = new SavedLoginStore(storage, () => throw new InvalidOperationException("Should not prompt"));
        Assert.Null(await store.UnlockAsync());
        Assert.Equal(0, storage.ReadCount);
    }

    [Fact]
    public async Task InvalidRestoredCredentialsAreRemoved()
    {
        var storage = new FakeStorage { Login = new SavedLogin("member@example.com", "") };
        var store = new SavedLoginStore(storage, () => Task.FromResult(true));
        Assert.Null(await store.UnlockAsync());
        Assert.False(store.HasSavedLogin);
    }

    private sealed class FakeStorage : ISavedLoginStorage
    {
        public SavedLogin? Login { get; set; }
        public int ReadCount { get; private set; }
        public bool HasSavedLogin => Login != null;
        public Task SaveAsync(string email, string password) { Login = new(email, password); return Task.CompletedTask; }
        public Task<SavedLogin?> ReadAsync() { ReadCount++; return Task.FromResult(Login); }
        public void Remove() => Login = null;
    }
}
