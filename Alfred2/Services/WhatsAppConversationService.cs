using Alfred2.DBContext;
using Alfred2.Models;
using Microsoft.EntityFrameworkCore;

namespace Alfred2.Services;

/// <summary>
/// Orquesta una conversación entrante de WhatsApp: resuelve médico/paciente,
/// persiste el mensaje, clasifica la intención y responde (proponer u ofrecer turnos).
/// El endpoint solo valida la firma y parsea; toda la lógica vive acá.
/// </summary>
public class WhatsAppConversationService
{
    private readonly AppDbContext _db;
    private readonly IntentService _intents;
    private readonly GCalService _gcal;
    private readonly PendingSlotsService _pending;
    private readonly TwilioResponder _twilio;
    private readonly MetaResponder _meta;
    private readonly IConfiguration _cfg;
    private readonly ILogger<WhatsAppConversationService> _log;

    public WhatsAppConversationService(
        AppDbContext db, IntentService intents, GCalService gcal, PendingSlotsService pending,
        TwilioResponder twilio, MetaResponder meta, IConfiguration cfg, ILogger<WhatsAppConversationService> log)
    {
        _db = db; _intents = intents; _gcal = gcal; _pending = pending;
        _twilio = twilio; _meta = meta; _cfg = cfg; _log = log;
    }

    public async Task HandleAsync(IncomingWA incoming, string provider)
    {
        _log.LogInformation("WA in {Provider} {From}->{To}: {Text}", incoming.Provider, incoming.FromE164, incoming.ToE164, incoming.Text);

        var medico = await ResolveMedicoAsync(incoming.ToE164);
        if (medico == null) return;

        var paciente = await EnsurePacienteAsync(medico.Id, incoming.FromE164);
        var conv = await EnsureConversacionAsync(medico.Id, paciente.Id);
        await PersistMensajeAsync(conv.Id, DireccionMensaje.Entrante, incoming.Text);

        var intent = await _intents.ClassifyAsync(incoming.Text);
        _log.LogInformation("Intent detected: {Intent} whenTag={When}", intent.Intent, intent.WhenTag);

        if (intent.Intent == "solicitar_turno")
        {
            await ProponerSlotsAsync(provider, conv, medico, incoming.FromE164);
            return;
        }

        if (int.TryParse(incoming.Text.Trim(), out var opcion) &&
            await TryConfirmarSlotAsync(provider, conv, paciente, incoming.FromE164, opcion))
        {
            return;
        }

        var respuesta = !string.IsNullOrEmpty(intent.ClarifyCopy)
            ? intent.ClarifyCopy
            : "¿Querés pedir un turno? Decime, por ejemplo: 'turno martes 14:30'.";
        await SendAsync(provider, conv, incoming.FromE164, respuesta);
    }

    // ---------- Resolución de entidades ----------

    private async Task<Medico?> ResolveMedicoAsync(string toE164)
    {
        var medico = await _db.Medicos.FirstOrDefaultAsync(m => m.TelefonoE164 == toE164);
        if (medico == null && DevState.DefaultMedicoId.HasValue)
        {
            medico = await _db.Medicos.FindAsync(DevState.DefaultMedicoId.Value);
            if (medico != null)
                _log.LogWarning("Usando DevState.DefaultMedicoId={MedicoId} como fallback para To={To}", DevState.DefaultMedicoId, toE164);
        }
        medico ??= await _db.Medicos.FirstOrDefaultAsync(); // fallback primer médico
        return medico;
    }

    private async Task<Paciente> EnsurePacienteAsync(Guid medicoId, string fromE164)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.MedicoId == medicoId && p.TelefonoE164 == fromE164);
        if (paciente == null)
        {
            paciente = new Paciente
            {
                MedicoId = medicoId,
                TelefonoE164 = fromE164,
                NombreCompleto = "Paciente WhatsApp"
            };
            _db.Pacientes.Add(paciente);
            await _db.SaveChangesAsync();
        }
        return paciente;
    }

    private async Task<Conversacion> EnsureConversacionAsync(Guid medicoId, Guid pacienteId)
    {
        var conv = await _db.Conversaciones.FirstOrDefaultAsync(c => c.MedicoId == medicoId && c.PacienteId == pacienteId);
        if (conv == null)
        {
            conv = new Conversacion
            {
                MedicoId = medicoId,
                PacienteId = pacienteId,
                UltimoMensajeUtc = DateTime.UtcNow
            };
            _db.Conversaciones.Add(conv);
        }
        else
        {
            conv.UltimoMensajeUtc = DateTime.UtcNow;
            _db.Conversaciones.Update(conv);
        }
        await _db.SaveChangesAsync();
        return conv;
    }

    // ---------- Flujo de turnos ----------

    private async Task ProponerSlotsAsync(string provider, Conversacion conv, Medico medico, string toE164)
    {
        var slots = (await _gcal.GetNextSlotsAsync(medico.Id, 3)).ToList();
        _log.LogInformation("Proposed {Count} slots", slots.Count);
        if (slots.Count == 0)
        {
            await SendAsync(provider, conv, toE164, "No tengo disponibilidad por ahora. ¿Otro día?");
            return;
        }

        _pending.Set(toE164, medico.Id, slots.Select(s => (s.StartUtc, s.EndUtc)));
        var msg = $"Tengo estas opciones ({CalMode()}):\n" +
                  $"1) {TimeZoneHelper.ToArDisplay(slots[0].StartUtc)}\n" +
                  $"2) {TimeZoneHelper.ToArDisplay(slots[1].StartUtc)}\n" +
                  $"3) {TimeZoneHelper.ToArDisplay(slots[2].StartUtc)}\nRespondé 1, 2 o 3.";
        await SendAsync(provider, conv, toE164, msg);
    }

    /// <summary>Devuelve true si la opción numérica fue manejada (confirmada o vencida).</summary>
    private async Task<bool> TryConfirmarSlotAsync(string provider, Conversacion conv, Paciente paciente, string toE164, int opcion)
    {
        if (_pending.TryGetValid(toE164, out var pend) && opcion >= 1 && opcion <= pend.Slots.Count)
        {
            var choice = pend.Slots[opcion - 1];
            var eventId = await CrearEventoAsync(pend.MedicoId, paciente.NombreCompleto, choice);
            await PersistirTurnoSiCorrespondeAsync(pend.MedicoId, paciente.Id, choice, eventId);
            _pending.Clear(toE164);

            _log.LogInformation("Event created id={EventId} (mode={CalMode})", eventId, CalMode());
            await SendAsync(provider, conv, toE164, $"Listo, reservé tu turno para {TimeZoneHelper.ToArDisplay(choice.startUtc)}. ¡Gracias!");
            return true;
        }

        if (opcion >= 1 && opcion <= 3)
        {
            await SendAsync(provider, conv, toE164, "Las opciones vencieron. Escribí 'turno' para ver disponibilidad actualizada.");
            return true;
        }

        return false;
    }

    private async Task<string> CrearEventoAsync(Guid medicoId, string pacienteNombre, (DateTime startUtc, DateTime endUtc) choice)
    {
        if (CalMode() == "simulate")
        {
            _log.LogInformation("CALENDAR_MODE=simulate → creando id simulado");
            return $"simulated-{Guid.NewGuid():N}";
        }
        _log.LogInformation("CALENDAR_MODE=real → llamando GCalService.CreateEventAsync");
        return await _gcal.CreateEventAsync(medicoId, pacienteNombre, choice.startUtc, choice.endUtc);
    }

    private async Task PersistirTurnoSiCorrespondeAsync(Guid medicoId, Guid pacienteId, (DateTime startUtc, DateTime endUtc) choice, string eventId)
    {
        if (!PersistTurnos())
        {
            _log.LogInformation("PERSIST_TURNOS=false → no se persiste el turno");
            return;
        }

        var turno = new Turno
        {
            MedicoId = medicoId,
            PacienteId = pacienteId,
            ServicioId = null,
            InicioUtc = choice.startUtc,
            FinUtc = choice.endUtc,
            Estado = EstadoTurno.Confirmado,
            Origen = OrigenTurno.WhatsApp,
            NotasInternas = $"GCAL {eventId}"
        };
        _db.Turnos.Add(turno);
        await _db.SaveChangesAsync();
        _log.LogInformation("PERSIST_TURNOS=true → Turno {TurnoId} creado para médico {MedicoId}", turno.Id, medicoId);
    }

    // ---------- Mensajería ----------

    private async Task SendAsync(string provider, Conversacion conv, string to, string text)
    {
        if (provider == "twilio") await _twilio.SendTextAsync(to, text);
        else await _meta.SendTextAsync(to, text);
        await PersistMensajeAsync(conv.Id, DireccionMensaje.Saliente, text);
    }

    private async Task PersistMensajeAsync(Guid conversacionId, DireccionMensaje direccion, string texto)
    {
        _db.Mensajes.Add(new Mensaje
        {
            ConversacionId = conversacionId,
            Direccion = direccion,
            Texto = texto,
            EnviadoUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    // ---------- Feature flags ----------

    private string CalMode() => (_cfg["FeatureFlags:CALENDAR_MODE"] ?? "simulate").Trim().ToLowerInvariant();

    private bool PersistTurnos() =>
        string.Equals((_cfg["FeatureFlags:PERSIST_TURNOS"] ?? "true").Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
