using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Alfred2.Services;

public class GoogleOAuthService
{
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _httpFactory;

    public GoogleOAuthService(IConfiguration cfg, IHttpClientFactory httpFactory)
    {
        _cfg = cfg; _httpFactory = httpFactory;
    }

    public string GetConsentUrl(string? userEmail = null)
    {
        var clientId = _cfg["GOOGLE_CLIENT_ID"] ?? string.Empty;
        var redirectUri = _cfg["GCAL_REDIRECT_URI"] ?? string.Empty;
        var scopes = _cfg["GCAL_SCOPES"] ?? "https://www.googleapis.com/auth/calendar.events";
        var state = string.IsNullOrEmpty(userEmail) ? "" : $"&state={Uri.EscapeDataString(userEmail)}";
        var url = $"https://accounts.google.com/o/oauth2/v2/auth?response_type=code&client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString(scopes)}&access_type=offline&prompt=consent&include_granted_scopes=true{state}";
        return url;
    }

    public async Task<(string accessToken, string? refreshToken, DateTime? expiresUtc)> ExchangeCodeAsync(string code)
    {
        var clientId = _cfg["GOOGLE_CLIENT_ID"] ?? string.Empty;
        var clientSecret = _cfg["GOOGLE_CLIENT_SECRET"] ?? string.Empty;
        var redirectUri = _cfg["GCAL_REDIRECT_URI"] ?? string.Empty;
        var http = _httpFactory.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });
        var res = await http.PostAsync("https://oauth2.googleapis.com/token", form);
        var body = await res.Content.ReadAsStringAsync();
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var at = root.GetProperty("access_token").GetString()!;
        var rt = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0;
        var expiresUtc = expiresIn > 0 ? DateTime.UtcNow.AddSeconds(expiresIn) : (DateTime?)null;
        return (at, rt, expiresUtc);
    }

    public async Task<(string accessToken, DateTime? expiresUtc)> RefreshAsync(string refreshToken)
    {
        var clientId = _cfg["GOOGLE_CLIENT_ID"] ?? string.Empty;
        var clientSecret = _cfg["GOOGLE_CLIENT_SECRET"] ?? string.Empty;
        var http = _httpFactory.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token"
        });
        var res = await http.PostAsync("https://oauth2.googleapis.com/token", form);
        var body = await res.Content.ReadAsStringAsync();
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var at = root.GetProperty("access_token").GetString()!;
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0;
        var expiresUtc = expiresIn > 0 ? DateTime.UtcNow.AddSeconds(expiresIn) : (DateTime?)null;
        return (at, expiresUtc);
    }
}
