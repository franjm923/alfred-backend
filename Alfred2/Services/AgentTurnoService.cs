using Alfred2.DBContext;
using Alfred2.Models;
using Microsoft.EntityFrameworkCore;

namespace Alfred2.Services;

/// <summary>Pedido del agente para crear un turno.</summary>
public record CrearTurnoRequest(Guid MedicoId, Guid PacienteId, DateTime InicioUtc, int DuracionMin = 30);

/// <summary>Se lanza cuando el médico referenciado no existe.</summary>
public class MedicoNoEncontradoException : Exception
{
    public MedicoNoEncontradoException(Guid medicoId)
        : base($"No existe un médico con id {medicoId}.") { }
}

/// <summary>Se lanza cuando el horario pedido se solapa con un turno existente del médico.</summary>
public class HorarioOcupadoException : Exception
{
    public HorarioOcupadoException(DateTime inicioUtc)
        : base($"Ya hay un turno que se solapa con el horario {inicioUtc:o}.") { }
}

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

        var finUtc = req.InicioUtc.AddMinutes(req.DuracionMin);

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
            InicioUtc = req.InicioUtc,
            FinUtc = finUtc,
            Estado = EstadoTurno.Confirmado,
        };

        _db.Turnos.Add(turno);
        await _db.SaveChangesAsync();
        return turno;
    }
}
