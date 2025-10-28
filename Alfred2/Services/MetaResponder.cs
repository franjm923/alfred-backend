using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Alfred2.Services;

public class MetaResponder
{
    private readonly HttpClient _http;
    private readonly string _pageToken;
    private readonly string _phoneId;

    public MetaResponder(IConfiguration cfg, IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient();
        _pageToken = cfg["WH_META_TOKEN"] ?? string.Empty;
        _phoneId   = cfg["WH_META_PHONE_ID"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_pageToken) || string.IsNullOrWhiteSpace(_phoneId))
            throw new InvalidOperationException("Faltan credenciales Meta Cloud API");

        _http.BaseAddress = new Uri($"https://graph.facebook.com/v20.0/{_phoneId}/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _pageToken);
    }

    public async Task SendTextAsync(string toE164, string text)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = toE164,
            type = "text",
            text = new { body = text }
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _http.PostAsync("messages", content);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new Exception($"Meta SendTextAsync {res.StatusCode}: {body}");
        }
    }
}
