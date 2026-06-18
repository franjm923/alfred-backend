using Alfred2.DBContext;
using Alfred2.Models;
using Microsoft.EntityFrameworkCore;

namespace Alfred2.Services;

/// <summary>Pedido del admin para registrar/actualizar un médico de prueba.</summary>
public record RegistrarMedicoPruebaRequest(Guid? MedicoId, string Numero, string? NombreCompleto = null);

/// <summary>
/// Operaciones de administración sobre médicos. Hoy: registrar un médico de prueba
/// (Id + número) para que el admin pueda operar como ese médico en el caso de prueba.
/// </summary>
public class AdminMedicoService
{
    private readonly AppDbContext _db;

    public AdminMedicoService(AppDbContext db) => _db = db;

    public async Task<Medico> RegistrarMedicoDePruebaAsync(RegistrarMedicoPruebaRequest req)
    {
        Medico? medico = null;
        if (req.MedicoId is Guid id)
            medico = await _db.Medicos.FirstOrDefaultAsync(m => m.Id == id);
        medico ??= await _db.Medicos.FirstOrDefaultAsync(m => m.TelefonoE164 == req.Numero);

        if (medico is null)
        {
            medico = new Medico
            {
                NombreCompleto = req.NombreCompleto ?? "Médico de prueba",
                Email = $"medico-{req.Numero}@alfred.local",
                TelefonoE164 = req.Numero,
            };
            if (req.MedicoId is Guid forcedId) medico.Id = forcedId;
            _db.Medicos.Add(medico);
        }
        else
        {
            medico.TelefonoE164 = req.Numero;
            if (!string.IsNullOrWhiteSpace(req.NombreCompleto))
                medico.NombreCompleto = req.NombreCompleto;
        }

        await _db.SaveChangesAsync();
        return medico;
    }
}
