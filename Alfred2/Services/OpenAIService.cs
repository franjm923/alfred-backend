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
        private readonly string _apiKey;

        public OpenAIChatService(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _apiKey = cfg["OPENAI_API_KEY"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        public async Task<ExtraccionDTO> ExtraerAsync(string userText, Solicitud borradorActual)
        {
            // 1) Prompts mínimos
            var system = "Eres un asistente de pedidos/turnos por WhatsApp. " +
                        "Extraes {tipo, producto, cantidad, direccion, formaPago, nombre}. " +
                        "Si falta algo, pide SOLO un dato por turno, tono breve rioplatense.";
            var context = $"Borrador actual: tipo={borradorActual?.Tipo.ToString().ToLower()}; " +
                        $"producto={borradorActual?.Producto}; cantidad={borradorActual?.Cantidad}; " +
                        $"direccion={borradorActual?.Direccion}; formaPago={borradorActual?.FormaPago}; " +
                        $"nombre={borradorActual?.NombreCliente}.";

            // 2) Payload con Structured Outputs (JSON Schema)
            var payload = new
            {
                model = "gpt-4o-mini",
                input = new object[] {
                    new { role = "system", content = system },
                    new { role = "user",   content = context },
                    new { role = "user",   content = userText }
                },
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "solicitud_schema",
                        schema = new
                        {
                            type = "object",
                            properties = new
                            {
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
                }
                // Si querés memoria:
                // ,store = true, previous_response_id = "resp_xxx"
            };

            var req = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("https://api.openai.com/v1/responses", req);  // <-- _http
            var txt = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                throw new Exception($"OpenAI {res.StatusCode}: {txt}");

            return ParseExtraccionFromResponses(txt, borradorActual);

        }
        private static readonly JsonSerializerOptions _jsonOpts =
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private static ExtraccionDTO ParseExtraccionFromResponses(string json, Solicitud borradorActual)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 1) Caso ideal: viene output_parsed (algunos clientes lo exponen)
            if (root.TryGetProperty("output_parsed", out var parsed) &&
                parsed.ValueKind != JsonValueKind.Null &&
                parsed.ValueKind != JsonValueKind.Undefined)
            {
                var dto = JsonSerializer.Deserialize<ExtraccionDTO>(parsed.GetRawText(), _jsonOpts);
                return dto ?? new ExtraccionDTO { Faltan = new() { "direccion" }, Copy = "¿A qué dirección lo mandamos?" };
            }

            // 2) Recorrer 'output' -> 'content' y buscar JSON estructurado
            if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in output.EnumerateArray())
                {
                    if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var c in content.EnumerateArray())
                        {
                            if (!c.TryGetProperty("type", out var typeEl)) continue;
                            var type = typeEl.GetString();

                            // a) output_json -> campo "json"
                            if (type == "output_json" && c.TryGetProperty("json", out var jsonEl))
                            {
                                var dto = JsonSerializer.Deserialize<ExtraccionDTO>(jsonEl.GetRawText(), _jsonOpts);
                                if (dto != null) return dto;
                            }

                            // b) output_text que contenga un JSON crudo
                            if (type == "output_text" && c.TryGetProperty("text", out var textEl))
                            {
                                var text = textEl.GetString();
                                if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("{"))
                                {
                                    try
                                    {
                                        var dto = JsonSerializer.Deserialize<ExtraccionDTO>(text, _jsonOpts);
                                        if (dto != null) return dto;
                                    }
                                    catch { /* ignorar y seguir */ }
                                }
                            }
                        }
                    }
                }
            }

            // 3) Fallback: pedimos dirección
            return new ExtraccionDTO { Faltan = new() { "direccion" }, Copy = "¿A qué dirección lo mandamos?" };
        }
    }
