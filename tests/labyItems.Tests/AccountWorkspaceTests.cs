using System.Net;
using System.Security.Cryptography;
using System.Text;
using labyItems.Services;
using labyItems.Models;
using labyItems.Models.Calendar;
using Microsoft.Data.Sqlite;
using Xunit;

namespace labyItems.Tests;

public sealed class AccountWorkspaceTests
{
    [Fact]
    public async Task AccountsReopenOnlyTheirOwnEncryptedDataAndGuestsHaveNoMemberData()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var seed = Path.Combine(root, "public.db");
            await File.WriteAllTextAsync(seed, "public reference data");
            var session = new LabyrintheSession(new HttpClient(new LoginHandler()));
            var keys = new Dictionary<string, byte[]>();
            Task<byte[]> Key(string account)
            {
                if (!keys.TryGetValue(account, out var key)) keys[account] = key = RandomNumberGenerator.GetBytes(32);
                return Task.FromResult(key.ToArray());
            }
            var workspace = new AccountWorkspace(session, Path.Combine(root, "vaults"), Path.Combine(root, "working"), seed, Key);
            await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.OpenAsync());
            await session.LoginAsync("alice@example.com", "alice-password");
            await workspace.OpenAsync();
            var aliceDirectory = workspace.RequireDirectory();
            await File.WriteAllTextAsync(Path.Combine(aliceDirectory, "private.txt"), "ALICE PRIVATE CHARACTER RECORD");
            workspace.Checkpoint();
            session.InvalidateOperations();
            Assert.Throws<InvalidOperationException>(() => workspace.RequireDirectory());
            workspace.Close();
            session.Logout();
            Assert.False(Directory.Exists(aliceDirectory));
            Assert.Null(session.Account);
            Assert.Throws<InvalidOperationException>(() => workspace.RequireDatabase());
            var archive = await File.ReadAllBytesAsync(Path.Combine(root, "vaults", AccountWorkspace.AccountKey("alice@example.com") + ".vault"));
            Assert.DoesNotContain("ALICE PRIVATE CHARACTER RECORD", Encoding.UTF8.GetString(archive));

            await session.LoginAsync("bob@example.com", "bob-password");
            await workspace.OpenAsync();
            Assert.False(File.Exists(Path.Combine(workspace.RequireDirectory(), "private.txt")));
            Assert.Throws<InvalidOperationException>(() => workspace.RequireDirectory("alice@example.com"));
            await File.WriteAllTextAsync(Path.Combine(workspace.RequireDirectory(), "private.txt"), "BOB RECORD");
            workspace.Close(); session.Logout();

            session.LoginAsGuest();
            await workspace.OpenAsync();
            Assert.False(File.Exists(Path.Combine(workspace.RequireDirectory(), "private.txt")));
            workspace.Close(); session.Logout();

            await session.LoginAsync("alice@example.com", "alice-password");
            await workspace.OpenAsync();
            Assert.Equal("ALICE PRIVATE CHARACTER RECORD", await File.ReadAllTextAsync(Path.Combine(workspace.RequireDirectory(), "private.txt")));
            workspace.Close(); session.Logout();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task MultipleProfilesPersistAndPasswordUpdatesInvalidateTheOldVerifier()
    {
        string? json = null;
        var cache = new AccountProfileCache(() => Task.FromResult(json), value => { json = value; return Task.CompletedTask; });
        await cache.SaveAsync("alice@example.com", "old", new("Alice", "Member"));
        await cache.SaveAsync("bob@example.com", "secret", new("Bob", "Member"));
        Assert.Equal("Alice Member", (await cache.GetAsync("alice@example.com", "old"))!.DisplayName);
        Assert.Equal("Bob Member", (await cache.GetAsync("bob@example.com", "secret"))!.DisplayName);
        Assert.Null(await cache.GetAsync("alice@example.com", "secret"));
        await cache.SaveAsync("alice@example.com", "new", new("Alice", "Member"));
        Assert.Null(await cache.GetAsync("alice@example.com", "old"));
        Assert.NotNull(await cache.GetAsync("alice@example.com", "new"));
        Assert.NotNull(await cache.GetAsync("bob@example.com", "secret"));
    }

    [Fact]
    public async Task LogoutInvalidatesAnOutstandingNetworkResponse()
    {
        var handler = new DelayedHandler();
        var session = new LabyrintheSession(new HttpClient(handler));
        await session.LoginAsync("alice@example.com", "password");
        var pending = session.GetHtmlAsync("/members/bookings/");
        await handler.Started.Task;
        session.Logout();
        handler.Complete.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("private data") });
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task ActualCharacterDatabaseIsScopedAndUnavailableAfterLogout()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previousWorkspace = AccountDataAccess.Workspace;
        var previousProvider = LiteDbService.PersonalDatabasePathProvider;
        AccountWorkspace? workspace = null;
        try
        {
            var seed = Path.Combine(root, "public.db");
            using (var connection = new SqliteConnection($"Data Source={seed};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE public_reference (name TEXT);";
                command.ExecuteNonQuery();
            }
            var session = new LabyrintheSession(new HttpClient(new LoginHandler()));
            var keys = new Dictionary<string, byte[]>();
            Task<byte[]> Key(string name)
            {
                if (!keys.TryGetValue(name, out var key)) keys[name] = key = RandomNumberGenerator.GetBytes(32);
                return Task.FromResult(key.ToArray());
            }
            workspace = new AccountWorkspace(session, Path.Combine(root, "vaults"), Path.Combine(root, "working"), seed, Key);
            AccountDataAccess.Workspace = workspace;
            LiteDbService.PersonalDatabasePathProvider = workspace.RequireDatabase;
            await session.LoginAsync("alice@example.com", "password");
            await workspace.OpenAsync();
            LiteDbService.UpsertCharacter(new Character { Id = "alice-character", Name = "Alice Private Character" });
            Assert.Single(LiteDbService.GetCharacters());
            workspace.Close(); session.Logout();
            Assert.Throws<InvalidOperationException>(() => LiteDbService.GetCharacters().ToList());
            await session.LoginAsync("bob@example.com", "password");
            await workspace.OpenAsync();
            Assert.Empty(LiteDbService.GetCharacters());
            workspace.Close(); session.Logout();
            await session.LoginAsync("alice@example.com", "password");
            await workspace.OpenAsync();
            Assert.Equal("Alice Private Character", Assert.Single(LiteDbService.GetCharacters()).Name);
            workspace.Close(); session.Logout();
        }
        finally
        {
            workspace?.Close();
            AccountDataAccess.Workspace = previousWorkspace;
            LiteDbService.PersonalDatabasePathProvider = previousProvider;
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TamperedEncryptedArchivesCannotBeOpened()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var seed = Path.Combine(root, "public.db");
            await File.WriteAllTextAsync(seed, "reference");
            var session = new LabyrintheSession(new HttpClient(new LoginHandler()));
            var key = RandomNumberGenerator.GetBytes(32);
            var workspace = new AccountWorkspace(session, root, Path.Combine(root, "working"), seed, _ => Task.FromResult(key.ToArray()));
            await session.LoginAsync("alice@example.com", "password");
            await workspace.OpenAsync();
            workspace.Close();
            var path = Path.Combine(root, AccountWorkspace.AccountKey("alice@example.com") + ".vault");
            var bytes = await File.ReadAllBytesAsync(path);
            bytes[^1] ^= 1;
            await File.WriteAllBytesAsync(path, bytes);
            await Assert.ThrowsAnyAsync<CryptographicException>(() => workspace.OpenAsync());
            Assert.Throws<InvalidOperationException>(() => workspace.RequireDirectory());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PreviousSessionCannotPublishAnAsynchronousCacheResult()
    {
        var session = new LabyrintheSession(new HttpClient(new LoginHandler()));
        await session.LoginAsync("alice@example.com", "password");
        var version = session.Version;
        session.Logout();
        var published = false;
        Assert.Throws<InvalidOperationException>(() => session.Publish(version, () => published = true));
        Assert.False(published);
    }

    [Fact]
    public async Task ABookingQueuedBeforeSwitchingCannotRunAsTheNextAccount()
    {
        var session = new LabyrintheSession(new HttpClient(new LoginHandler()));
        await session.LoginAsync("alice@example.com", "password");
        var cache = new DelayedCache();
        var store = new CalendarEventStore(session, cache);
        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var load = store.LoadMonthAsync(month);
        await cache.Started.Task;
        var queuedBooking = store.JoinRoleAsync("746", CalendarEventRole.Player);
        session.Logout();
        await session.LoginAsync("bob@example.com", "password");
        cache.Complete.SetResult(new CalendarCacheSnapshot());
        await Assert.ThrowsAsync<InvalidOperationException>(() => load);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queuedBooking);
        Assert.Null(store.GetEvent("746"));
    }

    private sealed class DelayedCache : ICalendarEventCache
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<CalendarCacheSnapshot?> Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<CalendarCacheSnapshot?> LoadAsync(string account) { Started.TrySetResult(true); return Complete.Task; }
        public Task SaveAsync(string account, CalendarCacheSnapshot snapshot) => Task.CompletedTask;
    }

    private sealed class LoginHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Found : HttpStatusCode.OK)
            { Content = new StringContent("<input name='field[first_name]' value='Member'><input name='field[last_name]' value='Name'>") });
    }
    private sealed class DelayedHandler : HttpMessageHandler
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpResponseMessage> Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath == "/members/bookings/") { Started.TrySetResult(true); return Complete.Task; }
            return Task.FromResult(new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Found : HttpStatusCode.OK)
            { Content = new StringContent("<input name='field[first_name]' value='Alice'><input name='field[last_name]' value='Member'>") });
        }
    }
}
