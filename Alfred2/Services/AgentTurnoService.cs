using Alfred2.DBContext;
using Alfred2.Domain.Exceptions;
using Alfred2.Models;
using Microsoft.EntityFrameworkCore;

namespace Alfred2.Services;

/// <summary>Pedido del agente para crear un turno.</summary>
public record CrearTurnoRequest(
    Guid MedicoId,
    Guid PacienteId,
    DateTime InicioUtc,
    int DuracionMin = 30,
    OrigenTurno Origen = OrigenTurno.Telegram,
    Guid? ServicioId = null);

/// <summary>
/// Core de dominio que el agente (n8n) invoca para operar turnos.
/// La superficie HTTP / API key / Telegram son cáscara fina alrededor de esto.
/// </summary>
public class AgentTurnoService
{
    private readonly AppDbContext _db;

    public AgentTurnoService(AppDbContext db) => _db = db;

    public async Task<Turno> CrearTurnoAsync(CrearTurnoRequest req)
    {
        if (!await _db.Medicos.AnyAsync(m => m.Id == req.MedicoId))
            throw new MedicoNoEncontradoException(req.MedicoId);

        // La duración sale del servicio (si se indicó uno válido del médico); si no, la del request.
        Servicio? servicio = null;
        if (req.ServicioId is Guid servicioId)
            servicio = await _db.Servicios.FirstOrDefaultAsync(s => s.Id == servicioId && s.MedicoId == req.MedicoId);

        var duracionMin = servicio?.DuracionMin ?? req.DuracionMin;
        var finUtc = req.InicioUtc.AddMinutes(duracionMin);

        var seSolapa = await _db.Turnos.AnyAsync(t =>
            t.MedicoId == req.MedicoId &&
            t.Estado != EstadoTurno.Cancelado &&
            t.InicioUtc < finUtc && t.FinUtc > req.InicioUtc);
        if (seSolapa)
            throw new HorarioOcupadoException(req.InicioUtc);

        var turno = new Turno
        {
            MedicoId = req.MedicoId,
            PacienteId = req.PacienteId,
            ServicioId = servicio?.Id,
            InicioUtc = req.InicioUtc,
            FinUtc = finUtc,
            Estado = EstadoTurno.Confirmado,
            Origen = req.Origen,
        };

        _db.Turnos.Add(turno);
        await _db.SaveChangesAsync();
        return turno;
    }
}
