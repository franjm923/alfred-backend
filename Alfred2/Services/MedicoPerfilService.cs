using Alfred2.DBContext;
using Alfred2.Domain.Exceptions;
using Alfred2.Models;
using Microsoft.EntityFrameworkCore;

namespace Alfred2.Services;

/// <summary>Datos del perfil que el médico carga (especialidad + matrícula).</summary>
public record GuardarPerfilRequest(string Email, string? Especialidad, string? Matricula);

/// <summary>Body del endpoint de perfil (el email sale del usuario autenticado, no del body).</summary>
public record ActualizarPerfilDto(string? Especialidad, string? Matricula);

/// <summary>
/// Self-service del médico: cargar su perfil y enviarlo a verificación.
/// Opera por email (el del usuario autenticado).
/// </summary>
public class MedicoPerfilService
{
    private readonly AppDbContext _db;

    public MedicoPerfilService(AppDbContext db) => _db = db;

    public async Task<Medico> GuardarPerfilAsync(GuardarPerfilRequest req)
    {
        var medico = await BuscarPorEmailAsync(req.Email);
        medico.Especialidad = req.Especialidad;
        medico.Matricula = req.Matricula;
        await _db.SaveChangesAsync();
        return medico;
    }

    public async Task<Medico> EnviarAVerificacionAsync(string email)
    {
        var medico = await BuscarPorEmailAsync(email);

        if (string.IsNullOrWhiteSpace(medico.Especialidad) || string.IsNullOrWhiteSpace(medico.Matricula))
            throw new PerfilIncompletoException("Cargá especialidad y matrícula antes de enviar a verificación.");

        medico.EstadoVerificacion = EstadoVerificacion.Pendiente;
        await _db.SaveChangesAsync();
        return medico;
    }

    private async Task<Medico> BuscarPorEmailAsync(string email)
        => await _db.Medicos.FirstOrDefaultAsync(m => m.Email == email)
           ?? throw new MedicoNoEncontradoException($"No existe un médico con email {email}.");
}
