using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Alfred2.DTOs;
using Alfred2.Models; // ModalidadTurno

namespace Alfred2.OpenAIService
{
    public class OpenAIChatService
    {
        private readonly HttpClient _http;

        private static readonly JsonSerializerOptions _jsonOpts =
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public OpenAIChatService(HttpClient http, IConfiguration cfg)
        {
            _http = http;

            // Auth y base URL
            var apiKey = cfg["OPENAI_API_KEY"] ?? cfg["OpenAI_API_KEY"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);
            }

            _http.BaseAddress ??= new Uri("https://api.openai.com/v1/");
            if (!_http.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
                _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Extrae datos de turno desde un texto libre (ES-AR).
        /// - serviciosDisponibles: lista de nombres (exactos) de Servicio.Nomre del médico (para ayudar al parser).
        /// - zonaHorariaIana: ej. "America/Argentina/Buenos_Aires" (solo para heurística local).
        /// Devuelve fecha/hora LOCAL en LocalInicio (sin tz).
        /// </summary>
        public async Task<ExtraccionTurnoDTO> ExtraerTurnoAsync(
            string userText,
            IEnumerable<string>? serviciosDisponibles = null,
            string? zonaHorariaIana = "America/Argentina/Buenos_Aires")
        {
            serviciosDisponibles ??= Enumerable.Empty<string>();

            // Prompt orientado a turno médico (no e-commerce)
            var serviciosList = serviciosDisponibles?.Any() == true
                ? "- Servicios disponibles: " + string.Join(", ", serviciosDisponibles) + "."
                : "- Servicios disponibles: (no listado).";

            var system =
                "Eres un asistente que extrae datos para agendar TURNOS médicos desde mensajes de WhatsApp en español rioplatense. " +
                "Debes devolver solo el JSON requerido. " +
                "Campos: servicio (string), localInicio (fecha y hora LOCAL, formato 'yyyy-MM-dd HH:mm'), duracionMin (int), modalidad ('presencial'|'virtual'), " +
                "nombre (string opcional del paciente), faltan (array de strings), copy (string breve para repreguntar). " +
                "Si faltan datos, completa 'faltan' y sugiere 'copy' pidiendo exactamente un dato. " +
                serviciosList;

            var userContext =
                "Si el usuario menciona el día de la semana (lunes..domingo) interpretarlo como la próxima ocurrencia a partir de hoy. " +
                "Si usa 'hoy' o 'mañana', resolver en local. Evitar zonas horarias en la salida. " +
                "Si no menciona servicio y hay más de uno en los servicios disponibles, dejar servicio=null y agregar 'servicio' en faltan. " +
                "Si no menciona hora o fecha, agregar 'fecha y hora' a faltan. " +
                "Si no menciona modalidad, no la pidas (puede quedar 'presencial' por defecto en la app).";

            // JSON schema para Structured Outputs
            var payload = new
            {
                model = "gpt-4o-mini",
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user",   content = userContext + "\n\n" + userText }
                },
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "turno_schema",
                        strict = true,
                        schema = new
                        {
                            type = "object",
                            properties = new
                            {
                                servicio = new { type = "string", nullable = true },
                                localInicio = new { type = "string", nullable = true, description = "Formato 'yyyy-MM-dd HH:mm' (LOCAL, sin tz)" },
                                duracionMin = new { type = "integer", nullable = true },
                                modalidad = new
                                {
                                    type = "string",
                                    nullable = true,
                                    // 'enum' es palabra reservada en C#; usar @enum
                                    @enum = new[] { "presencial", "virtual" }
                                },
                                nombre = new { type = "string", nullable = true },
                                faltan = new { type = "array", items = new { type = "string" } },
                                copy = new { type = "string", nullable = true }
                            },
                            required = new[] { "faltan" }
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

                var dto = ParseTurnoFromChatCompletions(txt);
                if (dto == null)
                    throw new Exception("No pude parsear la respuesta del modelo.");

                // Normalizaciones y parse de fecha/hora local
                dto = PostProcess(dto, serviciosDisponibles, zonaHorariaIana);
                return dto;
            }
            catch (Exception ex)
            {
                var msg = ex.ToString();

                // Fallback heurístico si 429 / quota / etc.
                if (msg.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("429"))
                {
                    return HeuristicExtractTurno(userText, serviciosDisponibles, zonaHorariaIana);
                }

                // Re-lanzar otros errores para que los maneje el controller
                throw;
            }
        }

        // ------------------ Parsers / Heurísticas ------------------

        private static ExtraccionTurnoDTO? ParseTurnoFromChatCompletions(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var msg = root.GetProperty("choices")[0].GetProperty("message");

            // 1) parsed (cuando usa json_schema con parsed)
            if (msg.TryGetProperty("parsed", out var parsed) &&
                parsed.ValueKind != JsonValueKind.Null &&
                parsed.ValueKind != JsonValueKind.Undefined)
            {
                return JsonSerializer.Deserialize<ExtraccionTurnoDTO>(parsed.GetRawText(), _jsonOpts);
            }

            // 2) content como string o array con bloques
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
                    try { return JsonSerializer.Deserialize<ExtraccionTurnoDTO>(contentStr, _jsonOpts); }
                    catch { /* ignore */ }
                }
            }
            return null;
        }

        private static ExtraccionTurnoDTO PostProcess(ExtraccionTurnoDTO dto, IEnumerable<string> servicios, string? tzId)
        {
            // Mapear modalidad string -> enum
            if (dto.Modalidad == null && dto.Copy == null)
            {
                // No forzamos si el LLM no lo puso; la app puede default a Presencial
            }

            // Parse fecha local si viene como string ISO esperado
            if (dto.LocalInicio == null && dto is { } && TryParseLocalIso(dto, out var local))
                dto.LocalInicio = local;

            // Normalizar servicio por coincidencia con la lista disponible (case-insensitive)
            if (!string.IsNullOrWhiteSpace(dto.Servicio) && servicios.Any())
            {
                var low = dto.Servicio!.Trim().ToLowerInvariant();
                var match = servicios.FirstOrDefault(s => string.Equals(s, dto.Servicio, StringComparison.OrdinalIgnoreCase)) ??
                            servicios.FirstOrDefault(s => s.ToLowerInvariant().Contains(low)) ??
                            servicios.FirstOrDefault(s => low.Contains(s.ToLowerInvariant()));

                if (!string.IsNullOrWhiteSpace(match))
                    dto.Servicio = match;
            }

            return dto;
        }

        private static bool TryParseLocalIso(ExtraccionTurnoDTO dto, out DateTime local)
        {
            local = default;
            if (dto.LocalInicio == null) return false;

            // Aceptamos 'yyyy-MM-dd HH:mm' o 'yyyy-MM-ddTHH:mm'
            var s = dto.LocalInicio?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            if (dto.LocalInicio.HasValue)
            {
                local = dto.LocalInicio.Value;
                return true;
            }
            return false;
        }

        private static ExtraccionTurnoDTO HeuristicExtractTurno(
            string text,
            IEnumerable<string> serviciosDisponibles,
            string? tzId)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new ExtraccionTurnoDTO { Faltan = new() { "fecha y hora" }, Copy = "¿Qué día y a qué hora querés el turno?" };

            var low = text.ToLowerInvariant();

            // Modalidad
            ModalidadTurno? modalidad = null;
            if (low.Contains("virtual") || low.Contains("videollamada") || low.Contains("online"))
                modalidad = ModalidadTurno.Virtual;
            else if (low.Contains("presencial") || low.Contains("consultorio"))
                modalidad = ModalidadTurno.Presencial;

            // Servicio por coincidencia simple
            string? servicio = null;
            foreach (var s in serviciosDisponibles)
            {
                if (low.Contains(s.ToLowerInvariant()))
                {
                    servicio = s;
                    break;
                }
            }

            // Fecha/hora local (heurística ES-AR)
            var local = TryParseFechaHoraEsAr(text, tzId) ?? (DateTime?)null;

            var faltan = new List<string>();
            if (local == null) faltan.Add("fecha y hora");
            if (servicio == null && serviciosDisponibles.Count() > 1) faltan.Add("servicio");

            var copy = faltan.Count > 0
                ? (faltan.Count == 2
                    ? "¿Para qué servicio y en qué horario te viene bien? Ej: \"Consulta general el martes 14:30\"."
                    : (faltan[0] == "servicio"
                        ? "¿Para qué servicio? Ej: \"consulta general\" o \"control\"."
                        : "¿Qué día y a qué hora? Ej: \"15/10 14:00\" o \"martes 10:30\"."))
                : "Perfecto, ya puedo agendarlo.";

            return new ExtraccionTurnoDTO
            {
                Servicio = servicio,
                LocalInicio = local,
                DuracionMin = null, // la app puede completar con la del servicio
                Modalidad = modalidad,
                Faltan = faltan,
                Copy = copy
            };
        }

        // --------- Utilidades de parsing ES-AR (texto libre) ---------

        // hoy/mañana, lunes..domingo, dd/MM, HH:mm (am/pm/hs opcional)
        private static DateTime? TryParseFechaHoraEsAr(string texto, string? tzId)
        {
            var now = DateTime.Now;
            var ci = new CultureInfo("es-AR");
            texto = texto.ToLowerInvariant();

            DateTime? fecha = null;

            if (texto.Contains("hoy")) fecha = now.Date;
            else if (texto.Contains("mañana")) fecha = now.Date.AddDays(1);
            else
            {
                string[] dias = { "domingo","lunes","martes","miércoles","miercoles","jueves","viernes","sábado","sabado" };
                for (int i = 0; i < dias.Length; i++)
                {
                    if (texto.Contains(dias[i]))
                    {
                        var target = (i == 0 || i == 7) ? DayOfWeek.Sunday :
                                     (i == 6 || i == 8) ? DayOfWeek.Saturday :
                                     (DayOfWeek)i;
                        fecha = NextDayOfWeek(now.Date, target);
                        break;
                    }
                }
            }

            // dd/MM o dd-MM (opcional año)
            var mFecha = Regex.Match(texto, @"\b(\d{1,2})[\/\-](\d{1,2})(?:[\/\-](\d{2,4}))?\b");
            if (mFecha.Success)
            {
                var d = int.Parse(mFecha.Groups[1].Value);
                var M = int.Parse(mFecha.Groups[2].Value);
                var y = mFecha.Groups[3].Success ? int.Parse(mFecha.Groups[3].Value) : now.Year;
                fecha = new DateTime(y, M, d);
            }

            // Hora HH:mm o HH.mm o HH hs
            DateTime? hora = null;
            var mHora = Regex.Match(texto, @"\b(\d{1,2})[:\.h\s]?(\d{2})?\s*(am|pm|hs)?\b");
            if (mHora.Success)
            {
                int h = int.Parse(mHora.Groups[1].Value);
                int min = mHora.Groups[2].Success ? int.Parse(mHora.Groups[2].Value) : 0;
                var suf = mHora.Groups[3].Success ? mHora.Groups[3].Value : null;
                if (string.Equals(suf, "pm", StringComparison.OrdinalIgnoreCase) && h < 12) h += 12;
                if (string.Equals(suf, "am", StringComparison.OrdinalIgnoreCase) && h == 12) h = 0;
                hora = now.Date.AddHours(h).AddMinutes(min);
            }

            if (fecha.HasValue && hora.HasValue)
            {
                return new DateTime(fecha.Value.Year, fecha.Value.Month, fecha.Value.Day,
                                    hora.Value.Hour, hora.Value.Minute, 0, DateTimeKind.Unspecified);
            }
            return null;

            static DateTime NextDayOfWeek(DateTime from, DayOfWeek day)
            {
                int diff = ((int)day - (int)from.DayOfWeek + 7) % 7;
                if (diff == 0) diff = 7;
                return from.AddDays(diff);
            }
        }
    }
}
