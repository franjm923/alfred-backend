using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Alfred2.Services;

public record IncomingWA(
    string Provider, // "twilio" | "meta"
    string FromE164,
    string ToE164,
    string MessageId,
    string Text,
    DateTime UtcNow
);

public class WhatsAppWebhookService
{
    public async Task<IncomingWA?> ParseAsync(HttpRequest req, string provider)
    {
        provider = provider?.Trim().ToLowerInvariant() ?? "";

        if (provider == "twilio")
        {
            // Twilio envía application/x-www-form-urlencoded
            if (!req.HasFormContentType)
                return null;

            var form = await req.ReadFormAsync();
            var from = form["From"].ToString();          // ej: whatsapp:+54911....
            var to = form["To"].ToString();              // ej: whatsapp:+14155238886
            var body = form["Body"].ToString();
            var sid = form["MessageSid"].ToString();

            return new IncomingWA(
                Provider: "twilio",
                FromE164: from.Replace("whatsapp:", string.Empty),
                ToE164: to.Replace("whatsapp:", string.Empty),
                MessageId: sid,
                Text: body ?? string.Empty,
                UtcNow: DateTime.UtcNow
            );
        }

        if (provider == "meta")
        {
            // Meta Cloud API envía JSON
            using var doc = await JsonDocument.ParseAsync(req.Body);
            var root = doc.RootElement;
            // Simplificado: tomamos el primer message
            try
            {
                var entry = root.GetProperty("entry")[0];
                var changes = entry.GetProperty("changes")[0];
                var value = changes.GetProperty("value");
                var messages = value.GetProperty("messages");
                var msg = messages[0];

                var from = msg.GetProperty("from").GetString() ?? ""; // E164 sin prefijo
                var text = msg.TryGetProperty("text", out var textEl)
                    ? (textEl.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null)
                    : msg.GetProperty("type").GetString();

                var messageId = msg.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                var to = value.TryGetProperty("metadata", out var md) && md.TryGetProperty("display_phone_number", out var toEl)
                    ? toEl.GetString()
                    : "";

                return new IncomingWA(
                    Provider: "meta",
                    FromE164: "+" + from.Trim('+'),
                    ToE164: to ?? string.Empty,
                    MessageId: messageId,
                    Text: text ?? string.Empty,
                    UtcNow: DateTime.UtcNow
                );
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
