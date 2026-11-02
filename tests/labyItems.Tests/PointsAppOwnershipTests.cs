using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using labyItems.Models.Documents;
using labyItems.Models.NonStandard;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class PointsAppOwnershipTests
{
    private const string Owner = "bradleyjamesbarfoot@gmail.com";

    [Theory]
    [InlineData(NonStandardEntityType.Ability)]
    [InlineData(NonStandardEntityType.Spell)]
    [InlineData(NonStandardEntityType.Miracle)]
    [InlineData(NonStandardEntityType.Evocation)]
    [InlineData(NonStandardEntityType.Neuronic)]
    [InlineData(NonStandardEntityType.CharacterClass)]
    [InlineData(NonStandardEntityType.CharacterRace)]
    public async Task DefinitionsAndLinkedDocumentsAreVisibleOnlyToTheirOwningMember(NonStandardEntityType kind)
    {
        using var fixture = new WorkspaceFixture();
        await fixture.SignIn(Owner);
        var ownerKey = PointsAppAccess.CurrentOwnerKey!;
        var request = new NonStandardSaveRequest
        {
            EntityType = kind, Name = "Private " + kind,
            OwnerAccountKey = ownerKey,
            DataJson = "{\"description\":\"Private points app\",\"ownerAccountKey\":\"forged-owner\"}",
            LifeScaleRaceName = "Human", LifeScaleClassName = "Warrior",
            LifeScalePoints = Enumerable.Range(1, 8).Select(_ => new LifeScalePoint(10, 5)).ToList()
        };
        await NonStandardContentService.SaveAsync(request);
        var entry = Assert.Single(await NonStandardContentService.GetWalletEntriesAsync());
        Assert.Equal(request.Name, entry.Name);
        using var json = JsonDocument.Parse(entry.DataJson);
        Assert.Equal(ownerKey, json.RootElement.GetProperty("ownerAccountKey").GetString());
        Assert.True(PointsAppAccess.CanSeeDefinition(entry.DataJson));
        var file = new DocumentReferenceCapture(DocumentReferenceKinds.LocalPath, "/private/points-app.pdf", "/private/points-app.pdf", null,
            "/private/points-app.pdf", "points-app.pdf", "application/pdf");
        await NonStandardContentService.SaveDocumentLinkAsync(kind, request.Name, "Approval", file, ownerKey);
        var document = Assert.Single(await NonStandardContentService.GetDocumentLinksAsync(kind, request.Name));
        Assert.True(NonStandardContentService.CanAccessDocument(document));
        Assert.False(NonStandardContentService.CanAccessDocument(document with { PersistentReference = "/private/someone-else.pdf" }));
        var staleRequest = new NonStandardSaveRequest { EntityType = kind, Name = "Stale editor", OwnerAccountKey = ownerKey };

        await fixture.SignIn("someone-else@example.com");
        Assert.Empty(await NonStandardContentService.GetWalletEntriesAsync());
        Assert.Empty(await NonStandardContentService.GetDocumentLinksAsync(kind, request.Name));
        Assert.False(PointsAppAccess.CanSeeDefinition(entry.DataJson));
        Assert.False(NonStandardContentService.CanAccessDocument(document));
        Assert.Throws<InvalidOperationException>(() => NonStandardContentService.RequireDocumentAccess(document));
        await Assert.ThrowsAsync<InvalidOperationException>(() => NonStandardContentService.SaveAsync(staleRequest));
        await Assert.ThrowsAsync<InvalidOperationException>(() => NonStandardContentService.SaveDocumentLinkAsync(kind, request.Name, "Stale upload", file, ownerKey));

        await fixture.Guest();
        Assert.Empty(await NonStandardContentService.GetWalletEntriesAsync());
        Assert.Empty(await NonStandardContentService.GetDocumentLinksAsync(kind, request.Name));
        Assert.False(PointsAppAccess.CanSeeDefinition(entry.DataJson));
        Assert.False(NonStandardContentService.CanAccessDocument(document));
        await Assert.ThrowsAsync<InvalidOperationException>(() => NonStandardContentService.SaveAsync(staleRequest));

        await fixture.Admin();
        Assert.False(PointsAppAccess.CanSeeDefinition(entry.DataJson));
        Assert.Empty(await NonStandardContentService.GetWalletEntriesAsync());
        Assert.False(NonStandardContentService.CanAccessDocument(document));

        await fixture.SignIn(Owner);
        Assert.True(PointsAppAccess.CanSeeDefinition(entry.DataJson));
        Assert.Equal(request.Name, Assert.Single(await NonStandardContentService.GetWalletEntriesAsync()).Name);
        Assert.True(NonStandardContentService.CanAccessDocument(document));
        await NonStandardContentService.DeleteDocumentLinkAsync(document.Id);
        Assert.False(NonStandardContentService.CanAccessDocument(document));
    }

    [Theory]
    [InlineData("{\"nonStandard\":true}")]
    [InlineData("{\"NonStandard\":\"true\"}")]
    [InlineData("{\"isNonStandard\":1}")]
    [InlineData("{\"ownerAccountKey\":\"someone-else\"}")]
    public void PrivateDefinitionsAreHiddenWithoutAnAuthenticatedMember(string json)
    {
        var previous = AccountDataAccess.Workspace;
        try
        {
            AccountDataAccess.Workspace = null;
            Assert.False(PointsAppAccess.CanSeeDefinition(json));
            Assert.True(PointsAppAccess.CanSeeDefinition("{\"name\":\"Public standard ability\"}"));
            Assert.Null(PointsAppAccess.CurrentOwnerKey);
        }
        finally { AccountDataAccess.Workspace = previous; }
    }

    private sealed class WorkspaceFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        private readonly AccountWorkspace? _previous = AccountDataAccess.Workspace;
        private readonly LabyrintheSession _session = new(new HttpClient(new LoginHandler()));
        private readonly AccountWorkspace _workspace;
        private readonly Dictionary<string, byte[]> _keys = new();
        public WorkspaceFixture()
        {
            Directory.CreateDirectory(_root);
            var seed = Path.Combine(_root, "public.db");
            LightweightMigrator.ApplyInitialSchema(seed);
            _workspace = new AccountWorkspace(_session, Path.Combine(_root, "vaults"), Path.Combine(_root, "working"), seed, Key);
            AccountDataAccess.Workspace = _workspace;
        }
        private Task<byte[]> Key(string account)
        {
            if (!_keys.TryGetValue(account, out var key)) _keys[account] = key = RandomNumberGenerator.GetBytes(32);
            return Task.FromResult(key.ToArray());
        }
        private void Close() { _workspace.Close(); _session.Logout(); }
        public async Task SignIn(string email) { Close(); Assert.True(await _session.LoginAsync(email, "member-password")); await _workspace.OpenAsync(); }
        public async Task Guest() { Close(); _session.LoginAsGuest(); await _workspace.OpenAsync(); }
        public async Task Admin() { Close(); Assert.True(await _session.LoginAsync("admin", "admin")); await _workspace.OpenAsync(); }
        public void Dispose() { _workspace.Close(); _session.Logout(); AccountDataAccess.Workspace = _previous; Directory.Delete(_root, true); }
    }
    private sealed class LoginHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Found : HttpStatusCode.OK)
            { Content = new StringContent("<input name='field[first_name]' value='Bradley'><input name='field[last_name]' value='Barfoot'>") });
    }
}
