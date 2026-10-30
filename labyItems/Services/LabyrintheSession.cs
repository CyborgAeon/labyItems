using labyItems.Models.Calendar;
using labyItems.Models.Authentication;
using System.Net;

namespace labyItems.Services;

/// <summary>One in-memory PHP session shared by authentication and event requests.</summary>
public sealed class LabyrintheSession
{
    public static LabyrintheSession Shared { get; } = new();
    private HttpClient _client;
    private readonly bool _ownsClient;
    private CookieContainer? _cookies;
    private SavedLogin? _offlineCredentials;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public AccountProfile? Account { get; private set; }
    public IAccountProfileCache? AccountProfiles { get; set; }
    public bool CanViewCalendar => IsAuthenticated && !IsFallback;
    public bool IsOfflineSession { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public bool IsFallback { get; private set; }
    public event Action? SessionChanged;

    public LabyrintheSession() { _client = CreateClient(); _ownsClient = true; }
    public LabyrintheSession(HttpClient client, CookieContainer? cookies = null)
    {
        _client = client;
        _cookies = cookies;
    }

    private HttpClient CreateClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = true,
        CookieContainer = _cookies = new CookieContainer()
    }) { Timeout = TimeSpan.FromSeconds(30) };

    public void LoginAsGuest()
    {
        if (_ownsClient)
        {
            _client.Dispose();
            _client = CreateClient();
        }
        IsOfflineSession = false;
        _offlineCredentials = null;
        Email = string.Empty;
        DisplayName = "Guest";
        Account = null;
        IsFallback = IsAuthenticated = true;
        SessionChanged?.Invoke();
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        IsAuthenticated = false;
        IsOfflineSession = false;
        _offlineCredentials = null;
        IsFallback = false;
        Email = string.Empty;
        DisplayName = string.Empty;
        Account = null;
        if (_ownsClient)
        {
            _client.Dispose();
            _client = CreateClient();
        }
        SessionChanged?.Invoke();
        // Retained explicitly for the requested local fallback; never sent to the website.
        if (email.Contains("admin", StringComparison.OrdinalIgnoreCase)
            && password.Contains("admin", StringComparison.OrdinalIgnoreCase))
        {
            IsFallback = IsAuthenticated = true;
            Email = email;
            DisplayName = "admin";
            return true;
        }
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email, ["password"] = password, ["submit_login"] = ""
        });
        HttpResponseMessage loginResponse;
        try { loginResponse = await _client.PostAsync("https://www.labyrinthe.co.uk/login/", content); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var cached = AccountProfiles == null ? null : await AccountProfiles.GetAsync(email, password);
            if (cached == null) throw;
            Email = email;
            Account = cached;
            DisplayName = cached.DisplayName;
            IsOfflineSession = IsAuthenticated = true;
            _offlineCredentials = new SavedLogin(email, password);
            return true;
        }
        using var response = loginResponse;
        IsAuthenticated = response.StatusCode == HttpStatusCode.Found;
        if (IsAuthenticated)
        {
            Email = email;
            try
            {
                Account = AccountProfiles == null ? null : await AccountProfiles.GetAsync(email, password);
                if (Account == null)
                {
                    var accountHtml = await GetHtmlAsync("/members/account/");
                    Account = LabyrintheEventParser.ParseAccountProfile(accountHtml);
                    if (AccountProfiles != null) await AccountProfiles.SaveAsync(email, password, Account);
                }
                DisplayName = Account.DisplayName;
            }
            catch
            {
                IsAuthenticated = false;
                Email = string.Empty;
                throw;
            }
        }
        return IsAuthenticated;
    }

    public async Task<string> GetHtmlAsync(string path)
    {
        if (!IsAuthenticated || IsFallback)
            throw new InvalidOperationException("Sign in with your Labyrinthe account to view live events.");
        await ReconnectAsync();
        using var response = await _client.GetAsync(new Uri(new Uri("https://www.labyrinthe.co.uk/"), path));
        if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Your session has expired. Please sign in again.");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        if (LabyrintheEventParser.IsLoginPage(html))
            throw new InvalidOperationException("Your session has expired. Please sign in again.");
        return html;
    }

    public async Task<bool> PostBookingAsync(string id, CalendarEventRole? role)
    {
        if (!IsAuthenticated || IsFallback || !id.All(char.IsAsciiDigit) || id.Length == 0) return false;
        await ReconnectAsync();
        var fields = new Dictionary<string, string>();
        if (role.HasValue)
        {
            var position = role.Value switch
            {
                CalendarEventRole.Player => "Player", CalendarEventRole.ARef => "A Referee",
                CalendarEventRole.Crew => "Crew", _ => throw new InvalidOperationException("Unsupported booking role.")
            };
            fields["staff_type"] = position;
            fields["position"] = position;
            if (role == CalendarEventRole.Player) fields["event_type"] = "Adult players";
            fields["submit_entry"] = "";
        }
        else fields["submit_cancel"] = "";
        // Both hosts belong to the same member site. Mirror host-scoped session cookies
        // for the booking endpoints, which use the bare host in the server contract.
        MirrorCookies("https://www.labyrinthe.co.uk/members/", "https://labyrinthe.co.uk/");
        using var content = new FormUrlEncodedContent(fields);
        using var response = await _client.PostAsync($"https://labyrinthe.co.uk/{(role.HasValue ? "" : "/")}members/event/{id}/", content);
        MirrorCookies("https://labyrinthe.co.uk/members/", "https://www.labyrinthe.co.uk/");
        if (response.StatusCode != HttpStatusCode.Found) return false;
        var location = response.Headers.Location;
        return location == null || !location.ToString().Contains("/login", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ReconnectAsync()
    {
        if (!IsOfflineSession || _offlineCredentials == null) return;
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = _offlineCredentials.Email, ["password"] = _offlineCredentials.Password, ["submit_login"] = ""
        });
        using var response = await _client.PostAsync("https://www.labyrinthe.co.uk/login/", form);
        if (response.StatusCode != HttpStatusCode.Found)
            throw new InvalidOperationException("Please sign in again to refresh your calendar.");
        IsOfflineSession = false;
        _offlineCredentials = null;
    }

    private void MirrorCookies(string source, string destination)
    {
        if (_cookies == null) return;
        foreach (Cookie cookie in _cookies.GetCookies(new Uri(source)))
            _cookies.Add(new Uri(destination), new Cookie(cookie.Name, cookie.Value, cookie.Path)
            { Secure = cookie.Secure, HttpOnly = cookie.HttpOnly });
    }

}
