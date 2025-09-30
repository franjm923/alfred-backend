using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using Alfred2.DBContext;
using Alfred2.Models;
using Alfred2.DTOs;

namespace Alfred2.Controladores
{
    [ApiController]
    [Route("api/bot")]
    public class BotController : ControllerBase
    {
        private readonly AppDbContext _db;

        public BotController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost("mensaje")]
        public async Task<IActionResult> Mensaje([FromBody] Inbound dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.TelefonoBot) ||
                string.IsNullOrWhiteSpace(dto?.Telefono)   ||
                string.IsNullOrWhiteSpace(dto?.Texto))
            {
                return BadRequest("Faltan datos mínimos (telefonoBot, telefono, texto).");
            }

            var botE164 = NormalizeE164(dto.TelefonoBot);
            var cliE164 = NormalizeE164(dto.Telefono);

            // 1) Resolver médico por número del bot
            var medico = await _db.Medicos.FirstOrDefaultAsync(m => m.TelefonoE164 == botE164);
            if (medico == null)
                return BadRequest("Bot no reconocido para ningún médico.");

            // 2) Paciente (o crear)
            var paciente = await _db.Pacientes.FirstOrDefaultAsync(p =>
                p.MedicoId == medico.Id && p.TelefonoE164 == cliE164);

            if (paciente == null)
            {
                paciente = new Paciente
                {
                    MedicoId = medico.Id,
                    TelefonoE164 = cliE164,
                    NombreCompleto = string.IsNullOrWhiteSpace(dto.Nombre) ? "Paciente" : dto.Nombre!.Trim()
                };
                _db.Pacientes.Add(paciente);
                await _db.SaveChangesAsync();
            }

            // 3) Conversación (1 por médico-paciente)
            var conv = await _db.Conversaciones.FirstOrDefaultAsync(c =>
                c.MedicoId == medico.Id &&
                c.PacienteId == paciente.Id &&
                c.Canal == CanalConversacion.WhatsApp);

            if (conv == null)
            {
                conv = new Conversacion
                {
                    MedicoId = medico.Id,
                    PacienteId = paciente.Id,
                    Canal = CanalConversacion.WhatsApp,
                    Estado = EstadoConversacion.Abierta,
                    NumeroRemitenteE164 = botE164,
                    NumeroPacienteE164 = cliE164,
                    UltimoMensajeUtc = DateTime.UtcNow
                };
                _db.Conversaciones.Add(conv);
                await _db.SaveChangesAsync();
            }

            // 4) Guardar mensaje entrante
            _db.Mensajes.Add(new Mensaje
            {
                ConversacionId = conv.Id,
                Direccion = DireccionMensaje.Entrante,
                Texto = dto.Texto.Trim(),
                EnviadoUtc = DateTime.UtcNow
            });
            conv.UltimoMensajeUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // 5) Intento de extraer turno desde texto
            var ext = await ExtraerTurnoBasicoAsync(medico, dto.Texto);

            // ¿Qué falta?
            var faltan = new List<string>();
            if (string.IsNullOrWhiteSpace(ext.Servicio) &&
                await _db.Servicios.CountAsync(s => s.MedicoId == medico.Id && s.Habilitado) > 1)
                faltan.Add("servicio");
            if (!ext.LocalInicio.HasValue) faltan.Add("fecha y hora");

            if (!ext.DuracionMin.HasValue)
                ext = ext with { DuracionMin = SugerirDuracion(medico, ext.Servicio) };
            if (!ext.Modalidad.HasValue)
                ext = ext with { Modalidad = ModalidadTurno.Presencial };

            string reply;

            if (faltan.Count > 0)
            {
                reply = ext.Copy ?? $"Para agendar necesito {string.Join(" y ", faltan)}. Ej: \"Consulta general el martes 14:30\".";
                await GuardarYResponder(conv, reply);
                return Ok(new { reply });
            }

            // Resolver servicio
            Servicio? servicio = null;
            var serviciosMed = await _db.Servicios.Where(s => s.MedicoId == medico.Id && s.Habilitado).ToListAsync();
            if (serviciosMed.Count == 1)
            {
                servicio = serviciosMed[0];
            }
            else if (!string.IsNullOrWhiteSpace(ext.Servicio))
            {
                servicio = serviciosMed.FirstOrDefault(s => s.Nombre.Equals(ext.Servicio, StringComparison.OrdinalIgnoreCase))
                           ?? serviciosMed.FirstOrDefault(s => s.Nombre.Contains(ext.Servicio!, StringComparison.OrdinalIgnoreCase));
            }

            var dur = ext.DuracionMin ?? servicio?.DuracionMin ?? 30;

            // Local -> UTC con TZ del médico
            var tz = GetTimeZone(medico.ZonaHorariaIana);
            var localInicio = DateTime.SpecifyKind(ext.LocalInicio!.Value, DateTimeKind.Unspecified);
            var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(localInicio, tz);
            var finUtc = inicioUtc.AddMinutes(dur);

            // Choques de agenda
            var solapa = await _db.Turnos.AnyAsync(t =>
                t.MedicoId == medico.Id &&
                t.Estado != EstadoTurno.Cancelado &&
                t.InicioUtc < finUtc && t.FinUtc > inicioUtc);

            if (solapa)
            {
                reply = "Ese horario ya está ocupado. ¿Probamos otro?";
                await GuardarYResponder(conv, reply);
                return Ok(new { reply });
            }

            // Bloqueos
            var bloqueado = await _db.Bloqueos.AnyAsync(b =>
                b.MedicoId == medico.Id &&
                b.InicioUtc < finUtc && b.FinUtc > inicioUtc);

            if (bloqueado)
            {
                reply = "Ese horario no está disponible por bloqueos de agenda. Probemos otro.";
                await GuardarYResponder(conv, reply);
                return Ok(new { reply });
            }

            // Crear turno Pendiente
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
                NotasInternas = $"Creado vía API {DateTime.UtcNow:O}"
            };
            _db.Turnos.Add(turno);
            await _db.SaveChangesAsync();

            var (fechaStr, horaStr) = FormatearFechaHora(localInicio);
            var srvLabel = servicio?.Nombre is null ? "" : $" ({servicio!.Nombre})";
            var mod = turno.Modalidad == ModalidadTurno.Virtual ? "virtual" : "presencial";

            reply = $"Listo {paciente.NombreCompleto.Split(' ').FirstOrDefault() ?? ""} 👌\n" +
                    $"Agendé un turno{srvLabel} el *{fechaStr}* a las *{horaStr}* ({mod}). " +
                    $"Cuando quieras lo confirmo con el doctor.";
            await GuardarYResponder(conv, reply);

            return Ok(new { reply });

            // helpers locales
            async Task GuardarYResponder(Conversacion c, string texto)
            {
                _db.Mensajes.Add(new Mensaje
                {
                    ConversacionId = c.Id,
                    Direccion = DireccionMensaje.Saliente,
                    Texto = texto,
                    EnviadoUtc = DateTime.UtcNow
                });
                c.UltimoMensajeUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        // ----------------- Extracción básica ES-AR -----------------

        private record TurnoExtract(string? Servicio, DateTime? LocalInicio, int? DuracionMin, ModalidadTurno? Modalidad, string? Copy);

        private async Task<TurnoExtract> ExtraerTurnoBasicoAsync(Medico medico, string texto)
        {
            var low = texto.ToLowerInvariant();

            // Modalidad
            ModalidadTurno? modalidad = null;
            if (low.Contains("virtual") || low.Contains("videollamada") || low.Contains("online"))
                modalidad = ModalidadTurno.Virtual;
            else if (low.Contains("presencial") || low.Contains("consultorio"))
                modalidad = ModalidadTurno.Presencial;

            // Servicio (match por nombre)
            string? servicio = null;
            var servicios = await _db.Servicios.Where(s => s.MedicoId == medico.Id && s.Habilitado).ToListAsync();
            if (servicios.Count == 1)
                servicio = servicios[0].Nombre;
            else
                servicio = servicios.FirstOrDefault(s => low.Contains(s.Nombre.ToLower()))?.Nombre;

            // Fecha/hora
            DateTime? localInicio = TryParseFechaHoraEsAr(texto, medico.ZonaHorariaIana);

            // Duración sugerida
            int? dur = servicios.FirstOrDefault(s => s.Nombre.Equals(servicio, StringComparison.OrdinalIgnoreCase))?.DuracionMin;

            // Copy faltante
            string? copy = null;
            if (localInicio == null)
                copy = "¿Qué día y a qué hora querés el turno? Ej: \"martes 10:30\" o \"15/10 14:00\".";
            else if (servicio == null && servicios.Count > 1)
                copy = "¿Para qué servicio? Ej: \"consulta general\" o \"control\".";

            return new TurnoExtract(servicio, localInicio, dur, modalidad, copy);
        }

        private static (string fecha, string hora) FormatearFechaHora(DateTime local)
        {
            var ci = new CultureInfo("es-AR");
            var fecha = local.ToString("dddd dd 'de' MMMM", ci);
            var hora  = local.ToString("HH:mm", ci);
            fecha = char.ToUpper(fecha[0], ci) + fecha.Substring(1);
            return (fecha, hora);
        }

        private static int SugerirDuracion(Medico m, string? servicio)
        {
            // MVP: 30' por defecto
            return 30;
        }

        private static TimeZoneInfo GetTimeZone(string iana)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(iana); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"); }
        }

        // hoy/mañana, lunes..domingo, dd/MM, HH:mm (am/pm/hs opcional)
        private static DateTime? TryParseFechaHoraEsAr(string texto, string tzId)
        {
            var now = DateTime.Now;
            var ci  = new CultureInfo("es-AR");
            var tz  = GetTimeZone(tzId);
            texto   = texto.ToLowerInvariant();

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

            var mFecha = Regex.Match(texto, @"\b(\d{1,2})[\/\-](\d{1,2})(?:[\/\-](\d{2,4}))?\b");
            if (mFecha.Success)
            {
                var d = int.Parse(mFecha.Groups[1].Value);
                var M = int.Parse(mFecha.Groups[2].Value);
                var y = mFecha.Groups[3].Success ? int.Parse(mFecha.Groups[3].Value) : now.Year;
                fecha = new DateTime(y, M, d);
            }

            DateTime? hora = null;
            var mHora = Regex.Match(texto, @"\b(\d{1,2})[:\.h\\s]?(\d{2})?\s*(am|pm|hs)?\b");
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