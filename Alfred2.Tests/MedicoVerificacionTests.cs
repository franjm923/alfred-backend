using Alfred2.DBContext;
using Alfred2.Domain.Exceptions;
using Alfred2.Models;
using Alfred2.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alfred2.Tests;

public class MedicoVerificacionTests
{
    private static AppDbContext NuevaDbEnMemoria() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<Medico> SembrarMedicoAsync(AppDbContext db, string email = "doc@test.com")
    {
        var medico = new Medico { NombreCompleto = "Dr. Test", Email = email };
        db.Medicos.Add(medico);
        await db.SaveChangesAsync();
        return medico;
    }

    [Fact]
    public async Task GuardarPerfil_actualizaEspecialidadYMatricula()
    {
        using var db = NuevaDbEnMemoria();
        await SembrarMedicoAsync(db);
        var service = new MedicoPerfilService(db);

        await service.GuardarPerfilAsync(new GuardarPerfilRequest("doc@test.com", "Clínica", "MN12345"));

        var medico = await db.Medicos.SingleAsync();
        Assert.Equal("Clínica", medico.Especialidad);
        Assert.Equal("MN12345", medico.Matricula);
    }

    [Fact]
    public async Task EnviarAVerificacion_conPerfilCompleto_pasaAPendiente()
    {
        using var db = NuevaDbEnMemoria();
        var medico = await SembrarMedicoAsync(db);
        medico.Especialidad = "Clínica";
        medico.Matricula = "MN12345";
        await db.SaveChangesAsync();
        var service = new MedicoPerfilService(db);

        await service.EnviarAVerificacionAsync("doc@test.com");

        var actualizado = await db.Medicos.SingleAsync();
        Assert.Equal(EstadoVerificacion.Pendiente, actualizado.EstadoVerificacion);
    }

    [Fact]
    public async Task EnviarAVerificacion_sinMatricula_lanzaYNoCambiaEstado()
    {
        using var db = NuevaDbEnMemoria();
        await SembrarMedicoAsync(db); // sin especialidad ni matrícula
        var service = new MedicoPerfilService(db);

        await Assert.ThrowsAsync<PerfilIncompletoException>(
            () => service.EnviarAVerificacionAsync("doc@test.com"));

        var medico = await db.Medicos.SingleAsync();
        Assert.Equal(EstadoVerificacion.Borrador, medico.EstadoVerificacion);
    }

    [Fact]
    public async Task Aprobar_pasaAAutorizado()
    {
        using var db = NuevaDbEnMemoria();
        var medico = await SembrarMedicoAsync(db);
        medico.EstadoVerificacion = EstadoVerificacion.Pendiente;
        await db.SaveChangesAsync();
        var admin = new AdminMedicoService(db);

        await admin.AprobarAsync(medico.Id);

        var actualizado = await db.Medicos.SingleAsync();
        Assert.Equal(EstadoVerificacion.Autorizado, actualizado.EstadoVerificacion);
    }

    [Fact]
    public async Task ListarPendientes_devuelveSoloLosPendientes()
    {
        using var db = NuevaDbEnMemoria();
        var pendiente = await SembrarMedicoAsync(db, "pendiente@test.com");
        pendiente.EstadoVerificacion = EstadoVerificacion.Pendiente;
        await SembrarMedicoAsync(db, "borrador@test.com"); // queda en Borrador
        await db.SaveChangesAsync();
        var admin = new AdminMedicoService(db);

        var pendientes = await admin.ListarPendientesAsync();

        Assert.Single(pendientes);
        Assert.Equal("pendiente@test.com", pendientes[0].Email);
    }
}
