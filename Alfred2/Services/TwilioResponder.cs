using System.Net.Http.Headers;
using System.Web;

namespace Alfred2.Services;

public class TwilioResponder
{
    private readonly HttpClient _http;
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _from;

    public TwilioResponder(IConfiguration cfg, IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient();
        _accountSid = cfg["TWILIO_ACCOUNT_SID"] ?? string.Empty;
        _authToken  = cfg["TWILIO_AUTH_TOKEN"]  ?? string.Empty;
        _from       = cfg["TWILIO_FROM"]        ?? ""; // whatsapp:+14155238886
        if (string.IsNullOrWhiteSpace(_accountSid) || string.IsNullOrWhiteSpace(_authToken))
            throw new InvalidOperationException("Faltan credenciales TWILIO");

        var byteArray = System.Text.Encoding.ASCII.GetBytes($"{_accountSid}:{_authToken}");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        _http.BaseAddress = new Uri($"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/");
    }

    public async Task SendTextAsync(string toE164, string text)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = _from,
            ["To"]   = $"whatsapp:{toE164}",
            ["Body"] = text
        });
        var res = await _http.PostAsync("Messages.json", content);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new Exception($"Twilio SendTextAsync {res.StatusCode}: {body}");
        }
    }
}

public class TwilioSignatureValidator
{
    private readonly string _authToken;
    public TwilioSignatureValidator(IConfiguration cfg)
    {
        _authToken = cfg["TWILIO_AUTH_TOKEN"] ?? string.Empty;
    }

    // Valida X-Twilio-Signature
    // Referencia: https://www.twilio.com/docs/usage/security#validating-requests
    public bool IsValid(Uri requestUrl, IFormCollection form, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(_authToken) || string.IsNullOrEmpty(signatureHeader))
            return false;

        // Construir la cadena: URL + parámetros form ordenados por nombre
        var sorted = form.OrderBy(kv => kv.Key).ToArray();
        var sb = new System.Text.StringBuilder();
        sb.Append(requestUrl.GetLeftPart(UriPartial.Path));
        foreach (var kv in sorted)
        {
            sb.Append(kv.Key);
            sb.Append(kv.Value);
        }

        // HMAC-SHA1
        using var hmac = new System.Security.Cryptography.HMACSHA1(System.Text.Encoding.UTF8.GetBytes(_authToken));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        var expected = Convert.ToBase64String(hash);
        return string.Equals(expected, signatureHeader, StringComparison.Ordinal);
    }
}
