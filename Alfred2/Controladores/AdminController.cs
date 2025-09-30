using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Alfred2.DBContext;
using Alfred2.Models;

namespace Alfred2.Controladores
{
    [ApiController]
    [Route("api/admin/turnos")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminController(AppDbContext db) { _db = db; }

        // GET api/admin/turnos/pendientes?medicoId=GUID
        [HttpGet("pendientes")]
        public async Task<IActionResult> Pendientes([FromQuery] Guid medicoId)
        {
            var medico = await _db.Medicos.FirstOrDefaultAsync(m => m.Id == medicoId);
            if (medico == null) return BadRequest("Médico no reconocido.");

            var tz = GetTimeZone(medico.ZonaHorariaIana);
            var ci = new CultureInfo("es-AR");

            var list = await _db.Turnos
                .Where(t => t.MedicoId == medicoId && t.Estado == EstadoTurno.Pendiente)
                .Include(t => t.Paciente)
                .Include(t => t.Servicio)
                .OrderBy(t => t.InicioUtc)
                .Select(t => new
                {
                    t.Id,
                    Paciente = t.Paciente.NombreCompleto,
                    Telefono = t.Paciente.TelefonoE164,
                    Servicio = t.Servicio != null ? t.Servicio.Nombre : null,
                    Modalidad = t.Modalidad.ToString(),
                    Estado = t.Estado.ToString(),
                    // Conversión a hora local del médico
                    Fecha = TimeZoneInfo.ConvertTimeFromUtc(t.InicioUtc, tz).ToString("yyyy-MM-dd", ci),
                    Hora  = TimeZoneInfo.ConvertTimeFromUtc(t.InicioUtc, tz).ToString("HH:mm", ci),
                    DuracionMin = (int)(t.FinUtc - t.InicioUtc).TotalMinutes,
                    PrecioSugerido = t.Servicio != null ? t.Servicio.Precio : null,
                    t.PrecioAcordado,
                    t.Motivo
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST api/admin/turnos/{id}/confirmar?medicoId=GUID
        [HttpPost("{id:guid}/confirmar")]
        public async Task<IActionResult> Confirmar(Guid id, [FromQuery] Guid medicoId, [FromBody] ConfirmarTurnoDTO dto)
        {
            var turno = await _db.Turnos
                .Include(t => t.Servicio)
                .FirstOrDefaultAsync(t => t.Id == id && t.MedicoId == medicoId && t.Estado == EstadoTurno.Pendiente);

            if (turno == null) return NotFound("Turno no encontrado o no está Pendiente.");

            if (dto.PrecioAcordado.HasValue) turno.PrecioAcordado = dto.PrecioAcordado.Value;
            if (dto.Modalidad.HasValue)      turno.Modalidad      = dto.Modalidad.Value;
            if (!string.IsNullOrWhiteSpace(dto.Notas))
                turno.NotasInternas = string.IsNullOrWhiteSpace(turno.NotasInternas)
                    ? dto.Notas!.Trim()
                    : $"{turno.NotasInternas}\n{dto.Notas!.Trim()}";

            turno.Estado = EstadoTurno.Confirmado;
            await _db.SaveChangesAsync();

            return Ok(new { ok = true });
        }

        // (Opcional) POST api/admin/turnos/{id}/cancelar?medicoId=GUID
        [HttpPost("{id:guid}/cancelar")]
        public async Task<IActionResult> Cancelar(Guid id, [FromQuery] Guid medicoId, [FromBody] CancelarTurnoDTO dto)
        {
            var turno = await _db.Turnos
                .FirstOrDefaultAsync(t => t.Id == id && t.MedicoId == medicoId && t.Estado != EstadoTurno.Cancelado);

            if (turno == null) return NotFound("Turno no encontrado o ya cancelado.");

            turno.Estado = EstadoTurno.Cancelado;
            if (!string.IsNullOrWhiteSpace(dto.Motivo))
                turno.NotasInternas = string.IsNullOrWhiteSpace(turno.NotasInternas)
                    ? $"Cancelado: {dto.Motivo!.Trim()}"
                    : $"{turno.NotasInternas}\nCancelado: {dto.Motivo!.Trim()}";

            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        private static TimeZoneInfo GetTimeZone(string iana)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(iana); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"); }
        }
    }

    // Podés mover estos DTOs a Alfred2.DTOs si preferís mantenerlos separados.
    public class ConfirmarTurnoDTO
    {
        public decimal? PrecioAcordado { get; set; }
        public ModalidadTurno? Modalidad { get; set; } // opcional cambiar a virtual/presencial al confirmar
        public string? Notas { get; set; }
    }

    public class CancelarTurnoDTO
    {
        public string? Motivo { get; set; }
    }
}
