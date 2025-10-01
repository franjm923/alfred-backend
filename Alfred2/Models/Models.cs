using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Alfred2.Models;

#region Enums
public enum EstadoTurno { Pendiente = 0, Confirmado = 1, Cancelado = 2, NoAsistio = 3, Completado = 4 }
public enum ModalidadTurno { Presencial = 0, Virtual = 1 }
public enum OrigenTurno { Manual = 0, WhatsApp = 1, Web = 2 }

public enum EstadoConversacion { Abierta = 0, Archivada = 1 }
public enum CanalConversacion { WhatsApp = 0 }

public enum DireccionMensaje { Entrante = 0, Saliente = 1 }

public enum EstadoSync { Nunca = 0, Ok = 1, Error = 2 }
#endregion

#region Base
public abstract class EntidadBase
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Timestamp UTC de creación</summary>
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp UTC de última modificación</summary>
    public DateTime? ModificadoUtc { get; set; }

    /// <summary>Soft-delete</summary>
    public bool Eliminado { get; set; }
}
#endregion

#region Núcleo MVP
/// <summary>
/// Usuario profesional. Reemplaza/renombra a "Usuario" (MVP Salud).
/// </summary>
[Index(nameof(Email), IsUnique = true)]
public class Medico : EntidadBase
{
    [Required, MaxLength(140)] public string NombreCompleto { get; set; } = string.Empty;
    [MaxLength(80)] public string? Especialidad { get; set; }
    [MaxLength(40)] public string? Matricula { get; set; }

    [Required, MaxLength(160)] public string Email { get; set; } = string.Empty;
    [MaxLength(32)] public string? TelefonoE164 { get; set; } // +54911...

    // Preferencias básicas MVP
    [MaxLength(64)] public string ZonaHorariaIana { get; set; } = "America/Argentina/Buenos_Aires";
    public bool AutoResponderHabilitado { get; set; } = true;
    [MaxLength(800)] public string? TextoAutoResponder { get; set; }

    public Guid UserId { get; set; } // relación 1–1 con User (opcional)
    public User User { get; set; } = null!;
    public Guid Id { get; set; }
    public ICollection<Paciente> Pacientes { get; set; } = new List<Paciente>();
    public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
    public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
    public ICollection<Conversacion> Conversaciones { get; set; } = new List<Conversacion>();
    public ICollection<DisponibilidadSemanal> Disponibilidades { get; set; } = new List<DisponibilidadSemanal>();
    public ICollection<BloqueoAgenda> Bloqueos { get; set; } = new List<BloqueoAgenda>();
    public ICollection<IntegracionCalendario> Integraciones { get; set; } = new List<IntegracionCalendario>();
}
[Index(nameof(Email), IsUnique = true)]
public class User : EntidadBase
{
    public Guid Id { get; set; }
    [Required, MaxLength(160)] public string Email { get; set; } = string.Empty;

    // Si usa login clásico
    public string? PasswordHash { get; set; }

    // Si usa login con Google
    [MaxLength(80)] public string? GoogleSub { get; set; } // el "sub" único que da Google
    public string? Name { get; set; }
    public string? Picture { get; set; }

    // Roles: medico / admin / staff
    [MaxLength(30)] public string Role { get; set; } = "medico";

    
   
    public Medico? Medico { get; set; }
}

/// <summary>
/// Paciente del médico. MVP: se mantiene 1:N (un paciente pertenece a un médico).
/// </summary>
[Index(nameof(MedicoId), nameof(TelefonoE164), IsUnique = true)]
public class Paciente : EntidadBase
{
    [Required] public Guid MedicoId { get; set; }
    public Medico Medico { get; set; } = null!;

    [Required, MaxLength(140)] public string NombreCompleto { get; set; } = string.Empty;
    [MaxLength(160)] public string? Email { get; set; }
    [Required, MaxLength(32)] public string TelefonoE164 { get; set; } = string.Empty; // Clave por WhatsApp

    [MaxLength(20)] public string? Documento { get; set; }
    [MaxLength(300)] public string? Notas { get; set; }

    public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
    public ICollection<Conversacion> Conversaciones { get; set; } = new List<Conversacion>();
}

/// <summary>
/// Tipo de prestación/servicio (p.ej. "Consulta general", 30 min, $X).
/// </summary>
[Index(nameof(MedicoId), nameof(Nombre), IsUnique = true)]
public class Servicio : EntidadBase
{
    [Required] public Guid MedicoId { get; set; }
    public Medico Medico { get; set; } = null!;

    [Required, MaxLength(100)] public string Nombre { get; set; } = string.Empty;
    [MaxLength(200)] public string? Descripcion { get; set; }
    [Range(5, 480)] public int DuracionMin { get; set; } = 30; // duración sugerida

    [Column(TypeName = "decimal(10,2)")] public decimal? Precio { get; set; }
    public bool Habilitado { get; set; } = true;

    public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
}

/// <summary>
/// Turno/cita médica. Reemplaza/renombra a "Solicitud" con campos de pedidos.
/// </summary>
[Index(nameof(MedicoId), nameof(InicioUtc))]
public class Turno : EntidadBase
{
    [Required] public Guid MedicoId { get; set; }
    public Medico Medico { get; set; } = null!;

    [Required] public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    public Guid? ServicioId { get; set; }
    public Servicio? Servicio { get; set; }

    [Required] public DateTime InicioUtc { get; set; }
    [Required] public DateTime FinUtc { get; set; }

    public EstadoTurno Estado { get; set; } = EstadoTurno.Pendiente;
    public ModalidadTurno Modalidad { get; set; } = ModalidadTurno.Presencial;
    public OrigenTurno Origen { get; set; } = OrigenTurno.WhatsApp;

    [MaxLength(240)] public string? Motivo { get; set; }
    [MaxLength(800)] public string? NotasInternas { get; set; }

    [Column(TypeName = "decimal(10,2)")] public decimal? PrecioAcordado { get; set; }
    [MaxLength(3)] public string? Moneda { get; set; } = "ARS";

    // Link a evento externo (Google Calendar, etc.)
    public ICollection<TurnoSyncCalendario> Sincronizaciones { get; set; } = new List<TurnoSyncCalendario>();
}

/// <summary>
/// Conversación (WhatsApp). Agrupa mensajes por paciente y médico.
/// </summary>
[Index(nameof(MedicoId), nameof(PacienteId), nameof(Canal))]
public class Conversacion : EntidadBase
{
    [Required] public Guid MedicoId { get; set; }
    public Medico Medico { get; set; } = null!;

    [Required] public Guid PacienteId { get; set; }
    public Paciente Paciente { get; set; } = null!;

    public CanalConversacion Canal { get; set; } = CanalConversacion.WhatsApp;
    public EstadoConversacion Estado { get; set; } = EstadoConversacion.Abierta;

    [MaxLength(64)] public string? NumeroRemitenteE164 { get; set; } // nro del médico (WA Business)
    [MaxLength(64)] public string? NumeroPacienteE164 { get; set; }

    public DateTime? UltimoMensajeUtc { get; set; }

    // IDs externos (Twilio SID, Meta WA msg/thread id, etc.)
    [MaxLength(128)] public string? ExternoThreadId { get; set; }

    public ICollection<Mensaje> Mensajes { get; set; } = new List<Mensaje>();
}

/// <summary>
/// Mensaje dentro de una conversación.
/// </summary>
[Index(nameof(ConversacionId), nameof(EnviadoUtc))]
public class Mensaje : EntidadBase
{
    [Required] public Guid ConversacionId { get; set; }
    public Conversacion Conversacion { get; set; } = null!;

    public DireccionMensaje Direccion { get; set; }

    [MaxLength(2000)] public string? Texto { get; set; }
    [MaxLength(512)] public string? MediaUrl { get; set; }

    public DateTime EnviadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EntregadoUtc { get; set; }
    public DateTime? LeidoUtc { get; set; }

    [MaxLength(128)] public string? ExternoMessageId { get; set; }
    [MaxLength(64)] public string? Plantilla { get; set; } // opcional: nombre de template usado
}

/// <summary>
/// Disponibilidad semanal del médico (patrón recurrente).
/// </summary>
[Index(nameof(MedicoId), nameof(DiaSemana))]
public class DisponibilidadSemanal : EntidadBase
{
    [Required] public Guid MedicoId { get; set; }
    public Medico Medico { get; set; } = null!;

    [Range(0,6)] public int DiaSemana { get; set; } // 0=Domingo..6=Sábado

    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }

    [Range(5, 480)] public int DuracionTurnoMin { get; set; } = 30;

    public bool Habilitado { get; set; } = true;
}

/// <summary>
/// Bloqueos puntuales de agenda (vacaciones, feriados, etc.).
/// </summary>
[Index(nameof(MedicoId), nameof(InicioUtc))]
public class BloqueoAgenda : EntidadBase
{
    [Required] public Guid MedicoId { get; set; }
    public Medico Medico { get; set; } = null!;

    [Required] public DateTime InicioUtc { get; set; }
    [Required] public DateTime FinUtc { get; set; }

    [MaxLength(200)] public string? Motivo { get; set; }
}

/// <summary>
/// Cuenta de integración de calendario (Google/Microsoft). Tokens deben almacenarse cifrados.
/// </summary>
[Index(nameof(MedicoId), nameof(Proveedor))]
public class IntegracionCalendario : EntidadBase
{
    [Required] public Guid MedicoId { get; set; }
    public Medico Medico { get; set; } = null!;

    [Required, MaxLength(40)] public string Proveedor { get; set; } = "Google"; // "Google" | "Microsoft"
    [MaxLength(128)] public string? ExternoAccountId { get; set; }

    // Guardar cifrado / seguro
    [MaxLength(2048)] public string? AccessTokenEnc { get; set; }
    [MaxLength(2048)] public string? RefreshTokenEnc { get; set; }
    public DateTime? ExpiraUtc { get; set; }

    public ICollection<TurnoSyncCalendario> Sincronizaciones { get; set; } = new List<TurnoSyncCalendario>();
}

/// <summary>
/// Mapeo Turno ↔ Evento en calendario externo (para idempotencia y re-sync).
/// </summary>
[Index(nameof(TurnoId), nameof(IntegracionCalendarioId), IsUnique = true)]
public class TurnoSyncCalendario : EntidadBase
{
    [Required] public Guid TurnoId { get; set; }
    public Turno Turno { get; set; } = null!;

    [Required] public Guid IntegracionCalendarioId { get; set; }
    public IntegracionCalendario IntegracionCalendario { get; set; } = null!;

    [MaxLength(128)] public string? ExternoCalendarId { get; set; }
    [MaxLength(128)] public string? ExternoEventId { get; set; }

    public EstadoSync Estado { get; set; } = EstadoSync.Nunca;
    [MaxLength(400)] public string? UltimoError { get; set; }
    public DateTime? UltimoSyncUtc { get; set; }
}
#endregion

