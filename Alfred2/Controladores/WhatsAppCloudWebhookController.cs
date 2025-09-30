
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alfred2.DBContext;
using Alfred2.Models;
using Microsoft.AspNetCore.Mvc;
using Alfred2.OpenAIService;
using Microsoft.EntityFrameworkCore;

namespace Alfred2.Controladores
{
    [ApiController]
    [Route("webhook/whatsapp")]
    public class WhatsAppCloudWebhookController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly IHttpClientFactory _httpFactory;
        private readonly AppDbContext _db;
        private readonly OpenAIChatService _llm; // lo seguimos usando si querés enriquecer prompts

        public WhatsAppCloudWebhookController(
            IConfiguration cfg,
            IHttpClientFactory httpFactory,
            AppDbContext db,
            OpenAIChatService llm)
        {
            _cfg = cfg; _httpFactory = httpFactory; _db = db; _llm = llm;
        }

        // 1) Verificación (Meta llama con GET una sola vez)
        [HttpGet]
        public IActionResult Verify(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string token,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            var verify = (_cfg.GetValue<string>("WHATSAPP_VERIFY_TOKEN") ?? "").Trim();
            var got = (token ?? "").Trim();
            Console.WriteLine($"[Webhook Verify] mode='{mode}', cfg='{verify}' got='{got}' challengeLen={challenge?.Length}");

            if (string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(challenge)
                && string.Equals(got, verify, StringComparison.Ordinal))
            {
                return Content(challenge, "text/plain", Encoding.UTF8);
            }
            return Unauthorized("Invalid verify token");
        }

        // 2) Mensajes entrantes
        [HttpPost]
        public async Task<IActionResult> Receive()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            Console.WriteLine($"[WA IN] {body}");
            using var doc = JsonDocument.Parse(body);

            var root = doc.RootElement;
            if (!root.TryGetProperty("entry", out var entries)) return Ok();

            foreach (var entry in entries.EnumerateArray())
            {
                foreach (var change in entry.GetProperty("changes").EnumerateArray())
                {
                    var value = change.GetProperty("value");

                    // Nº del bot (línea de negocio)
                    var botNumber = value.GetProperty("metadata").GetProperty("display_phone_number").GetString() ?? "";
                    var botE164 = NormalizeE164(botNumber);

                    if (!value.TryGetProperty("messages", out var messages)) continue;

                    foreach (var msg in messages.EnumerateArray())
                    {
                        var fromRaw = msg.GetProperty("from").GetString() ?? "";
                        var fromE164 = NormalizeE164(fromRaw);
                        var type = msg.GetProperty("type").GetString();

                        // Sólo texto para MVP
                        if (type != "text") continue;

                        var text = msg.GetProperty("text").GetProperty("body").GetString() ?? "";
                        var waMsgId = msg.TryGetProperty("id", out var mid) ? mid.GetString() : null;

                        // 1) Resolver médico por número del bot
                        var medico = await _db.Medicos.FirstOrDefaultAsync(m => m.TelefonoE164 == botE164);
                        if (medico == null)
                        {
                            Console.WriteLine($"[WARN] No hay Medico con TelefonoE164={botE164}");
                            continue;
                        }

                        // 2) Paciente por teléfono (crear si no existe)
                        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p =>
                            p.MedicoId == medico.Id && p.TelefonoE164 == fromE164);

                        if (paciente == null)
                        {
                            paciente = new Paciente
                            {
                                MedicoId = medico.Id,
                                TelefonoE164 = fromE164,
                                NombreCompleto = "Paciente"
                            };
                            _db.Pacientes.Add(paciente);
                            await _db.SaveChangesAsync();
                        }

                        // 3) Conversación (1 por médico-paciente-canal)
                        var conv = await _db.Conversaciones.FirstOrDefaultAsync(c =>
                            c.MedicoId == medico.Id && c.PacienteId == paciente.Id && c.Canal == CanalConversacion.WhatsApp);

                        if (conv == null)
                        {
                            conv = new Conversacion
                            {
                                MedicoId = medico.Id,
                                PacienteId = paciente.Id,
                                Canal = CanalConversacion.WhatsApp,
                                Estado = EstadoConversacion.Abierta,
                                NumeroRemitenteE164 = botE164,
                                NumeroPacienteE164 = fromE164,
                                UltimoMensajeUtc = DateTime.UtcNow
                            };
                            _db.Conversaciones.Add(conv);
                            await _db.SaveChangesAsync();
                        }

                        // 4) Guardar mensaje entrante
                        var inMsg = new Mensaje
                        {
                            ConversacionId = conv.Id,
                            Direccion = DireccionMensaje.Entrante,
                            Texto = text,
                            EnviadoUtc = DateTime.UtcNow,
                            ExternoMessageId = waMsgId
                        };
                        _db.Mensajes.Add(inMsg);
                        conv.UltimoMensajeUtc = DateTime.UtcNow;
                        await _db.SaveChangesAsync();

                        // 5) Manejar intención: agendar turno
                        var reply = await HandleIncomingTurnoAsync(medico, paciente, conv, text);
                        if (!string.IsNullOrWhiteSpace(reply))
                        {
                            var sentId = await SendWhatsAppAsync(fromE164, reply);
                            // guardar mensaje saliente
                            var outMsg = new Mensaje
                            {
                                ConversacionId = conv.Id,
                                Direccion = DireccionMensaje.Saliente,
                                Texto = reply,
                                EnviadoUtc = DateTime.UtcNow,
                                ExternoMessageId = sentId
                            };
                            _db.Mensajes.Add(outMsg);
                            conv.UltimoMensajeUtc = DateTime.UtcNow;
                            await _db.SaveChangesAsync();
                        }
                    }
                }
            }
            return Ok();
        }

        // ---------- LÓGICA DE NEGOCIO PARA TURNOS (MVP) --------------------

        private record TurnoExtract(string? Servicio, DateTime? LocalInicio, int? DuracionMin, ModalidadTurno? Modalidad, string? Nombre, string? Copy);

        private async Task<string> HandleIncomingTurnoAsync(Medico medico, Paciente paciente, Conversacion conv, string texto)
        {
            // 0) Heurística / extracción simple desde texto (ES-AR)
            var ext = await ExtraerTurnoBasicoAsync(medico, texto);

            // 1) Qué nos falta
            var faltan = new List<string>();
            if (string.IsNullOrWhiteSpace(ext.Servicio) && (await _db.Servicios.CountAsync(s => s.MedicoId == medico.Id)) > 1)
                faltan.Add("servicio");
            if (!ext.LocalInicio.HasValue) faltan.Add("fecha y hora");
            if (!ext.DuracionMin.HasValue) ext = ext with { DuracionMin = SugerirDuracion(medico, ext.Servicio) };
            if (!ext.Modalidad.HasValue) ext = ext with { Modalidad = ModalidadTurno.Presencial };

            if (faltan.Count > 0)
            {
                // Mensaje guía (puede venir de LLM o fijo)
                return ext.Copy ?? $"Para agendar necesito {string.Join(" y ", faltan)}. Ej: \"Consulta general el lunes 14:30\".";
            }

            // 2) Resolver servicio (si hay varios)
            Servicio? servicio = null;
            var serviciosMed = await _db.Servicios.Where(s => s.MedicoId == medico.Id && s.Habilitado).ToListAsync();
            if (serviciosMed.Count == 0)
            {
                // fallback sin servicio
            }
            else if (serviciosMed.Count == 1)
            {
                servicio = serviciosMed[0];
            }
            else if (!string.IsNullOrWhiteSpace(ext.Servicio))
            {
                servicio = serviciosMed.FirstOrDefault(s => string.Equals(s.Nombre, ext.Servicio, StringComparison.OrdinalIgnoreCase))
                           ?? serviciosMed.FirstOrDefault(s => s.Nombre.Contains(ext.Servicio!, StringComparison.OrdinalIgnoreCase));
            }

            var dur = ext.DuracionMin ?? servicio?.DuracionMin ?? 30;
            var tz = GetTimeZone(medico.ZonaHorariaIana);
            var localInicio = DateTime.SpecifyKind(ext.LocalInicio!.Value, DateTimeKind.Unspecified);
            var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(localInicio, tz);
            var finUtc = inicioUtc.AddMinutes(dur);

            // 3) Validaciones de disponibilidad y superposición
            var solapa = await _db.Turnos.AnyAsync(t =>
                t.MedicoId == medico.Id &&
                t.Estado != EstadoTurno.Cancelado &&
                t.InicioUtc < finUtc && t.FinUtc > inicioUtc);

            if (solapa)
                return "Ese horario ya está ocupado. ¿Querés probar con otro horario?";

            var bloqueado = await _db.Bloqueos.AnyAsync(b =>
                b.MedicoId == medico.Id &&
                b.InicioUtc < finUtc && b.FinUtc > inicioUtc);

            if (bloqueado)
                return "Ese horario no está disponible por bloqueos de agenda. Probemos otro.";

            // 4) Crear turno (Pendiente)
            var turno = new Turno
            {
                MedicoId = medico.Id,
                PacienteId = paciente.Id,
                ServicioId = servicio?.Id,
                InicioUtc = inicioUtc,
                FinUtc = finUtc,
                Estado = EstadoTurno.Pendiente,
                Modalidad = ext.Modalidad!.Value,
                Origen = OrigenTurno.WhatsApp,
                Motivo = servicio?.Nombre ?? "Consulta",
                PrecioAcordado = servicio?.Precio,
                NotasInternas = $"Creado vía WA {DateTime.UtcNow:O}"
            };
            _db.Turnos.Add(turno);
            await _db.SaveChangesAsync();

            // 5) Mensaje de confirmación
            var (fechaStr, horaStr) = FormatearFechaHora(localInicio);
            var srvLabel = servicio?.Nombre is null ? "" : $" ({servicio!.Nombre})";
            var mod = turno.Modalidad == ModalidadTurno.Virtual ? "virtual" : "presencial";

            return $"Listo {paciente.NombreCompleto.Split(' ').FirstOrDefault() ?? ""} 👌\n" +
                   $"Agendé un turno{srvLabel} el *{fechaStr}* a las *{horaStr}* ({mod}). " +
                   $"Cuando quieras, confirmo con el doctor.";
        }

        private async Task<TurnoExtract> ExtraerTurnoBasicoAsync(Medico medico, string texto)
        {
            // Modalidad
            ModalidadTurno? modalidad = null;
            var low = texto.ToLowerInvariant();
            if (low.Contains("virtual") || low.Contains("videollamada") || low.Contains("online"))
                modalidad = ModalidadTurno.Virtual;
            else if (low.Contains("presencial") || low.Contains("consultorio"))
                modalidad = ModalidadTurno.Presencial;

            // Servicio: buscar por nombre aproximado
            string? servicio = null;
            var servicios = await _db.Servicios.Where(s => s.MedicoId == medico.Id && s.Habilitado).ToListAsync();
            if (servicios.Count == 1)
                servicio = servicios[0].Nombre;
            else
            {
                foreach (var s in servicios)
                {
                    if (low.Contains(s.Nombre.ToLowerInvariant()))
                    {
                        servicio = s.Nombre;
                        break;
                    }
                }
            }

            // Fecha y hora (heurística rápida ES-AR)
            DateTime? localInicio = TryParseFechaHoraEsAr(texto, medico.ZonaHorariaIana);

            // Duración: sugerida por servicio
            int? dur = servicios.FirstOrDefault(s => s.Nombre.Equals(servicio, StringComparison.OrdinalIgnoreCase))?.DuracionMin;

            // Texto de ayuda si falta info
            string? copy = null;
            if (localInicio == null)
                copy = "¿Qué día y a qué hora querés el turno? Ej: \"martes 10:30\" o \"15/10 14:00\".";

            return new TurnoExtract(servicio, localInicio, dur, modalidad, null, copy);
        }

        private static (string fecha, string hora) FormatearFechaHora(DateTime local)
        {
            var ci = new CultureInfo("es-AR");
            var fecha = local.ToString("dddd dd 'de' MMMM", ci); // lunes 30 de septiembre
            var hora = local.ToString("HH:mm", ci);
            // Capitalizar primera letra del día
            fecha = char.ToUpper(fecha[0], ci) + fecha.Substring(1);
            return (fecha, hora);
        }

        private static int SugerirDuracion(Medico m, string? servicio)
        {
            if (!string.IsNullOrWhiteSpace(servicio))
                return 30;
            return 30; // default MVP
        }

        private static TimeZoneInfo GetTimeZone(string iana)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(iana); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"); }
        }

        // Parses básicos en español (hoy/mañana, lunes..domingo, dd/MM, HH:mm)
        private static DateTime? TryParseFechaHoraEsAr(string texto, string tzId)
        {
            var now = DateTime.Now;
            var ci = new CultureInfo("es-AR");
            var tz = GetTimeZone(tzId);
            texto = texto.ToLowerInvariant();

            // Palabras clave
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
                                     (DayOfWeek)i; // lunes=1, etc.
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
                var local = new DateTime(fecha.Value.Year, fecha.Value.Month, fecha.Value.Day,
                                         hora.Value.Hour, hora.Value.Minute, 0, DateTimeKind.Unspecified);
                return local;
            }

            return null;

            static DateTime NextDayOfWeek(DateTime from, DayOfWeek day)
            {
                int diff = ((int)day - (int)from.DayOfWeek + 7) % 7;
                if (diff == 0) diff = 7;
                return from.AddDays(diff);
            }
        }

        // -------- Envío por Cloud API y persistencia opcional del msg --------

        private async Task<string?> SendWhatsAppAsync(string toE164, string text)
        {
            var token = _cfg["WHATSAPP_TOKEN"];
            var phoneId = _cfg["WHATSAPP_PHONE_NUMBER_ID"];
            var client = _httpFactory.CreateClient("whatsapp");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var toDigits = NormalizeE164(toE164);
            if (string.IsNullOrWhiteSpace(toDigits))
            {
                Console.WriteLine("[WA SEND] Número destino vacío");
                return null;
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                to = toDigits, // 54911XXXXXXX
                type = "text",
                text = new { body = text }
            };

            var json = JsonSerializer.Serialize(payload);
            Console.WriteLine($"[WA SEND] -> to={toDigits} pid={phoneId} json={json}");

            var res = await client.PostAsync(
                $"https://graph.facebook.com/v23.0/{phoneId}/messages",
                new StringContent(json, Encoding.UTF8, "application/json"));

            var resBody = await res.Content.ReadAsStringAsync();
            Console.WriteLine($"[WA SEND] {(int)res.StatusCode} {res.StatusCode} {resBody}");

            try
            {
                using var doc = JsonDocument.Parse(resBody);
                var messages = doc.RootElement.GetProperty("messages");
                var id = messages.EnumerateArray().FirstOrDefault().GetProperty("id").GetString();
                return id;
            }
            catch { return null; }
        }

        private static string NormalizeE164(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            s = s.Replace("whatsapp:", "").Trim();
            if (s.StartsWith("+")) s = s[1..];
            var digits = new string(s.Where(char.IsDigit).ToArray());
            return digits;
        }
    }
}
