using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;


using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Alfred2.Models;
using Alfred2.DTOs;


namespace Alfred2.OpenAIService;

public class OpenAIChatService
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _jsonOpts =
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    public OpenAIChatService(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        // Si ya seteaste el Authorization en Program.cs, no hace falta repetirlo acá.
        var apiKey = cfg["OPENAI_API_KEY"] ?? cfg["OpenAI_API_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
        _http.BaseAddress ??= new Uri("https://api.openai.com/v1/");
        _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ExtraccionDTO> ExtraerAsync(string userText, Solicitud borradorActual)
    {

        var system =
            "Eres un asistente de pedidos/turnos por WhatsApp. " +
            "Extraes {tipo, producto, cantidad, direccion, formaPago, nombre}. " +
            "Si falta algo, pide SOLO un dato por turno, tono breve rioplatense.";

        var context =
            $"Borrador actual: tipo={borradorActual?.Tipo.ToString().ToLower()}; " +
            $"producto={borradorActual?.Producto}; cantidad={borradorActual?.Cantidad}; " +
            $"direccion={borradorActual?.Direccion}; formaPago={borradorActual?.FormaPago}; " +
            $"nombre={borradorActual?.NombreCliente}.";

        // CHAT COMPLETIONS con Structured Outputs
        var payload = new
        {
            model = "gpt-4o-mini",
            messages = new object[] {
                    new { role = "system", content = system },
                    new { role = "user",   content = $"{context}\n\n{userText}" }
                },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "solicitud_schema",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            // En C#, 'enum' es palabra reservada: se escribe @enum
                            tipo = new { type = "string", @enum = new[] { "pedido", "turno" }, nullable = true },
                            producto = new { type = "string", nullable = true },
                            cantidad = new { type = "integer", nullable = true },
                            direccion = new { type = "string", nullable = true },
                            formaPago = new { type = "string", nullable = true },
                            nombre = new { type = "string", nullable = true },
                            faltan = new { type = "array", items = new { type = "string" } },
                            copy = new { type = "string" }
                        },
                        required = new[] { "faltan", "copy" }
                    }
                }
            },
            temperature = 0.2
        };
        try
        {
            var req = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("chat/completions", req);
            var txt = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                throw new Exception($"OpenAI {res.StatusCode}: {txt}");

            return ParseFromChatCompletions(txt) ??
                   new ExtraccionDTO { Faltan = new() { "direccion" }, Copy = "¿A qué dirección lo mandamos?" };
        }
        catch (Exception ex)
        {
            var msg = ex.ToString();

            // Sin crédito / 429 / rate limits → fallback heurístico para que el bot no se caiga
            if (msg.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("429"))
            {
                return HeuristicExtract(userText, borradorActual);
            }

            throw; // otros errores (auth, red, etc.) → que los maneje el controlador
        }

    }
     private static ExtraccionDTO? ParseFromChatCompletions(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // choices[0].message
            var msg = root.GetProperty("choices")[0].GetProperty("message");

            // 1) Algunos SDKs agregan 'parsed' cuando se usa json_schema
            if (msg.TryGetProperty("parsed", out var parsed) &&
                parsed.ValueKind != JsonValueKind.Null &&
                parsed.ValueKind != JsonValueKind.Undefined)
            {
                return JsonSerializer.Deserialize<ExtraccionDTO>(parsed.GetRawText(), _jsonOpts);
            }

            // 2) Normalmente viene en 'content' como string JSON
            if (msg.TryGetProperty("content", out var contentEl))
            {
                string? contentStr = null;

                if (contentEl.ValueKind == JsonValueKind.String)
                {
                    contentStr = contentEl.GetString();
                }
                else if (contentEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in contentEl.EnumerateArray())
                    {
                        if (part.ValueKind == JsonValueKind.Object &&
                            part.TryGetProperty("type", out var t) &&
                            (t.GetString() == "text" || t.GetString() == "output_text") &&
                            part.TryGetProperty("text", out var textEl))
                        {
                            contentStr = textEl.GetString();
                            break;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(contentStr) && contentStr!.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<ExtraccionDTO>(contentStr, _jsonOpts);
                    }
                    catch
                    {
                        // si no pudo parsear, devolvemos null para ir al fallback del caller
                    }
                }
            }

            return null;
        }    
    private static ExtraccionDTO HeuristicExtract(string text, Solicitud borrador)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ExtraccionDTO { Faltan = new() { "direccion" }, Copy = "¿A qué dirección lo mandamos?" };

        text = text.ToLowerInvariant();

        // tipo
        string? tipo = null;
        if (text.Contains("turno")) tipo = "turno";
        if (text.Contains("pedido")) tipo = "pedido";

        // cantidad (primer número que aparezca)
        int? cantidad = null;
        var mCant = System.Text.RegularExpressions.Regex.Match(text, @"\b(\d{1,3})\b");
        if (mCant.Success && int.TryParse(mCant.Groups[1].Value, out var c)) cantidad = c;

        // pago
        string? formaPago = null;
        if (text.Contains("efectivo")) formaPago = "efectivo";
        else if (text.Contains("transfer")) formaPago = "transferencia";
        else if (text.Contains("mercado pago") || text.Contains("mp")) formaPago = "mercado pago";
        else if (text.Contains("tarjeta")) formaPago = "tarjeta";

        // nombre (busca “soy …”, “me llamo …”, “mi nombre es …”)
        string? nombre = null;
        var mNombre = System.Text.RegularExpressions.Regex.Match(text, @"(soy|me llamo|mi nombre es)\s+([a-záéíóúñ ]{2,30})");
        if (mNombre.Success) nombre = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(mNombre.Groups[2].Value.Trim());

        // dirección (busca “en …”, “a …”, “direc …” con calle y nro)
        string? direccion = null;
        var mDir = System.Text.RegularExpressions.Regex.Match(text, @"(?:en|a)\s+([a-záéíóúñ0-9\s\.]{5,40}\d{1,5})");
        if (mDir.Success) direccion = mDir.Groups[1].Value.Trim();

        // producto (lo que queda si hay “quiero …”, “pido …”, “necesito …”)
        string? producto = null;
        var mProd = System.Text.RegularExpressions.Regex.Match(text, @"(quiero|pido|necesito)\s+([a-záéíóúñ0-9\s\-]{3,40})");
        if (mProd.Success) producto = mProd.Groups[2].Value.Trim();

        // armamos faltantes
        var faltan = new List<string>();
        if (tipo == "pedido")
        {
            if (string.IsNullOrWhiteSpace(producto)) faltan.Add("producto");
            if (!cantidad.HasValue) faltan.Add("cantidad");
        }
        if (string.IsNullOrWhiteSpace(direccion)) faltan.Add("direccion");
        if (string.IsNullOrWhiteSpace(formaPago)) faltan.Add("formaPago");
        if (string.IsNullOrWhiteSpace(nombre)) faltan.Add("nombre");

        var copy = (faltan.Count == 0)
            ? "¡Listo! Registré tu solicitud. En breve te paso el total con envío."
            : $"Me falta {string.Join(", ", faltan)}. ¿Me lo pasás?";

        return new ExtraccionDTO
        {
            Tipo = tipo,
            Producto = producto,
            Cantidad = cantidad,
            Direccion = direccion,
            FormaPago = formaPago,
            Nombre = nombre,
            Faltan = faltan,
            Copy = copy
        };
    }
}
