using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;

using Alfred2.DTOs;
using Alfred2.Models;


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
                                tipo      = new { type = "string", @enum = new[] { "pedido", "turno" }, nullable = true },
                                producto  = new { type = "string",  nullable = true },
                                cantidad  = new { type = "integer", nullable = true },
                                direccion = new { type = "string",  nullable = true },
                                formaPago = new { type = "string",  nullable = true },
                                nombre    = new { type = "string",  nullable = true },
                                faltan    = new { type = "array",   items = new { type = "string" } },
                                copy      = new { type = "string" }
                            },
                            required = new[] { "faltan", "copy" }
                        }
                    }
                },
                temperature = 0.2
            };

            var req = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("chat/completions", req);
            var txt = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                throw new Exception($"OpenAI {res.StatusCode}: {txt}");

            // Parse para Chat Completions + Structured Outputs
            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;
            var msg = root.GetProperty("choices")[0].GetProperty("message");

            // 1) Si viene 'parsed', usarlo directo
            if (msg.TryGetProperty("parsed", out var parsed) &&
                parsed.ValueKind != JsonValueKind.Null &&
                parsed.ValueKind != JsonValueKind.Undefined)
            {
                var dtoOk = JsonSerializer.Deserialize<ExtraccionDTO>(parsed.GetRawText(), _jsonOpts);
                if (dtoOk != null) return dtoOk;
            }

            // 2) Si viene como texto JSON en 'content'
            string? content = null;
            if (msg.TryGetProperty("content", out var contentEl))
            {
                if (contentEl.ValueKind == JsonValueKind.String)
                {
                    content = contentEl.GetString();
                }
                else if (contentEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in contentEl.EnumerateArray())
                    {
                        if (part.TryGetProperty("type", out var t) && part.TryGetProperty("text", out var tval))
                        {
                            var ts = t.GetString();
                            if ((ts == "text" || ts == "output_text"))
                            {
                                content = tval.GetString();
                                break;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(content) && content!.TrimStart().StartsWith("{"))
            {
                try
                {
                    var dto = JsonSerializer.Deserialize<ExtraccionDTO>(content, _jsonOpts);
                    if (dto != null) return dto;
                }
                catch { /* seguir al fallback */ }
            }

            // 3) Fallback
            return new ExtraccionDTO { Faltan = new() { "direccion" }, Copy = "¿A qué dirección lo mandamos?" };
        }
    }
