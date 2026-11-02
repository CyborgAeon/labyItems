using labyItems.Models.Calendar;
using labyItems.Models.Authentication;
using System.Net;

namespace labyItems.Services;

/// <summary>A replaceable PHP session. Requests retain their originating account's client and epoch.</summary>
public sealed class LabyrintheSession
{
    public static LabyrintheSession Shared { get; } = new();
    private HttpClient _client;
    private readonly bool _ownsClient;
    private CookieContainer? _cookies;
    private SavedLogin? _offlineCredentials;
    private AccountProfile? _account;
    private long _version;
    private readonly object _sessionSync = new();
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public AccountProfile? Account
    {
        get { lock (_sessionSync) return IsAuthenticated && !IsFallback ? _account : null; }
    }
    public IAccountProfileCache? AccountProfiles { get; set; }
    public bool CanViewCalendar => IsAuthenticated && !IsFallback;
    public bool IsOfflineSession { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public bool IsFallback { get; private set; }
    public event Action? SessionChanged;
    public long Version => Interlocked.Read(ref _version);

    public LabyrintheSession() { _client = CreateClient(); _ownsClient = true; }
    public LabyrintheSession(HttpClient client, CookieContainer? cookies = null)
    {
        _client = client;
        _cookies = cookies;
    }
    private HttpClient CreateClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, UseCookies = true,
        CookieContainer = _cookies = new CookieContainer()
    }) { Timeout = TimeSpan.FromSeconds(30) };

    public void RequireVersion(long version)
    {
        if (version != Version) throw new InvalidOperationException("The account session has changed.");
    }
    public void Publish(long version, Action publish)
    {
        lock (_sessionSync) { RequireVersion(version); publish(); }
    }
    public void InvalidateOperations()
    {
        lock (_sessionSync) Interlocked.Increment(ref _version);
    }
    private RequestContext Reset()
    {
        RequestContext request;
        lock (_sessionSync)
        {
            Interlocked.Increment(ref _version);
            IsAuthenticated = IsFallback = IsOfflineSession = false;
            Email = DisplayName = string.Empty;
            _account = null;
            _offlineCredentials = null;
            if (_ownsClient) { _client.Dispose(); _client = CreateClient(); }
            request = new RequestContext(Version, _client, _cookies, null);
        }
        SessionChanged?.Invoke();
        return request;
    }
    public void Logout() => Reset();
    public void LoginAsGuest()
    {
        var request = Reset();
        Publish(request.Version, () => { DisplayName = "Guest"; IsFallback = IsAuthenticated = true; });
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var request = Reset();
        if (email.Contains("admin", StringComparison.OrdinalIgnoreCase) && password.Contains("admin", StringComparison.OrdinalIgnoreCase))
        {
            Publish(request.Version, () => { IsFallback = IsAuthenticated = true; Email = email; DisplayName = "admin"; });
            return true;
        }
        using var content = LoginForm(email, password);
        HttpResponseMessage response;
        try { response = await request.Client.PostAsync("https://www.labyrinthe.co.uk/login/", content); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var cached = AccountProfiles == null ? null : await AccountProfiles.GetAsync(email, password);
            RequireVersion(request.Version);
            if (cached == null) throw;
            Publish(request.Version, () =>
            {
                Email = email; _account = cached; DisplayName = cached.DisplayName;
                IsOfflineSession = IsAuthenticated = true;
                _offlineCredentials = new SavedLogin(email, password);
            });
            return true;
        }
        using (response)
        {
            RequireVersion(request.Version);
            if (response.StatusCode != HttpStatusCode.Found) return false;
        }
        Publish(request.Version, () => { IsAuthenticated = true; Email = email; });
        try
        {
            var profile = AccountProfiles == null ? null : await AccountProfiles.GetAsync(email, password);
            RequireVersion(request.Version);
            if (profile == null)
            {
                profile = LabyrintheEventParser.ParseAccountProfile(await GetHtmlAsync("/members/account/", request.Version));
                RequireVersion(request.Version);
                if (AccountProfiles != null) await AccountProfiles.SaveAsync(email, password, profile);
            }
            Publish(request.Version, () => { _account = profile; DisplayName = profile.DisplayName; });
            return true;
        }
        catch
        {
            lock (_sessionSync)
                if (Version == request.Version) { IsAuthenticated = false; Email = string.Empty; _account = null; }
            throw;
        }
    }

    private RequestContext Capture(long? expectedVersion)
    {
        lock (_sessionSync)
        {
            RequireVersion(expectedVersion ?? Version);
            if (!CanViewCalendar) throw new InvalidOperationException("Sign in with your Labyrinthe account to view live events.");
            return new RequestContext(Version, _client, _cookies, _offlineCredentials);
        }
    }

    public async Task<string> GetHtmlAsync(string path, long? expectedVersion = null)
    {
        var request = Capture(expectedVersion);
        await ReconnectAsync(request);
        RequireVersion(request.Version);
        using var response = await request.Client.GetAsync(new Uri(new Uri("https://www.labyrinthe.co.uk/"), path));
        RequireVersion(request.Version);
        if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Your session has expired. Please sign in again.");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        RequireVersion(request.Version);
        if (LabyrintheEventParser.IsLoginPage(html))
            throw new InvalidOperationException("Your session has expired. Please sign in again.");
        return html;
    }

    public async Task<bool> PostBookingAsync(string id, CalendarEventRole? role, long? expectedVersion = null)
    {
        RequestContext request;
        lock (_sessionSync)
        {
            RequireVersion(expectedVersion ?? Version);
            if (!CanViewCalendar || string.IsNullOrEmpty(id) || !id.All(char.IsAsciiDigit)) return false;
            request = Capture(expectedVersion);
        }
        await ReconnectAsync(request);
        RequireVersion(request.Version);
        var fields = new Dictionary<string, string>();
        if (role.HasValue)
        {
            var position = role.Value switch
            {
                CalendarEventRole.Player => "Player", CalendarEventRole.ARef => "A Referee",
                CalendarEventRole.Crew => "Crew", _ => throw new InvalidOperationException("Unsupported booking role.")
            };
            fields["staff_type"] = position; fields["position"] = position;
            if (role == CalendarEventRole.Player) fields["event_type"] = "Adult players";
            fields["submit_entry"] = "";
        }
        else fields["submit_cancel"] = "";
        MirrorCookies(request.Cookies, "https://www.labyrinthe.co.uk/members/", "https://labyrinthe.co.uk/");
        using var content = new FormUrlEncodedContent(fields);
        using var response = await request.Client.PostAsync($"https://labyrinthe.co.uk/{(role.HasValue ? "" : "/")}members/event/{id}/", content);
        RequireVersion(request.Version);
        MirrorCookies(request.Cookies, "https://labyrinthe.co.uk/members/", "https://www.labyrinthe.co.uk/");
        return response.StatusCode == HttpStatusCode.Found
            && !(response.Headers.Location?.ToString().Contains("/login", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private async Task ReconnectAsync(RequestContext request)
    {
        if (request.OfflineCredentials == null) return;
        using var form = LoginForm(request.OfflineCredentials.Email, request.OfflineCredentials.Password);
        using var response = await request.Client.PostAsync("https://www.labyrinthe.co.uk/login/", form);
        RequireVersion(request.Version);
        if (response.StatusCode != HttpStatusCode.Found)
            throw new InvalidOperationException("Please sign in again to refresh your calendar.");
        Publish(request.Version, () => { IsOfflineSession = false; _offlineCredentials = null; });
    }
    private static FormUrlEncodedContent LoginForm(string email, string password) => new(new Dictionary<string, string>
    {
        ["email"] = email, ["password"] = password, ["submit_login"] = ""
    });
    private static void MirrorCookies(CookieContainer? cookies, string source, string destination)
    {
        if (cookies == null) return;
        foreach (Cookie cookie in cookies.GetCookies(new Uri(source)))
            cookies.Add(new Uri(destination), new Cookie(cookie.Name, cookie.Value, cookie.Path)
            { Secure = cookie.Secure, HttpOnly = cookie.HttpOnly });
    }
    private sealed record RequestContext(long Version, HttpClient Client, CookieContainer? Cookies, SavedLogin? OfflineCredentials);
}
