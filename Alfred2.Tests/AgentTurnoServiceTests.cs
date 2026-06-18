using Alfred2.DBContext;
using Alfred2.Models;
using Alfred2.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alfred2.Tests;

public class AgentTurnoServiceTests
{
    private static AppDbContext NuevaDbEnMemoria() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Medico medico, Paciente paciente)> SembrarMedicoYPacienteAsync(AppDbContext db)
    {
        var medico = new Medico { NombreCompleto = "Dra. Ana", Email = "ana@test.com" };
        var paciente = new Paciente { MedicoId = medico.Id, NombreCompleto = "Juan", TelefonoE164 = "5491100000000" };
        db.Medicos.Add(medico);
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        return (medico, paciente);
    }

    [Fact]
    public async Task CrearTurnoAsync_conDatosValidos_persisteTurnoConCamposCorrectos()
    {
        // Arrange
        using var db = NuevaDbEnMemoria();
        var (medico, paciente) = await SembrarMedicoYPacienteAsync(db);
        var inicioUtc = new DateTime(2030, 1, 10, 14, 0, 0, DateTimeKind.Utc);
        var service = new AgentTurnoService(db);

        // Act
        var turno = await service.CrearTurnoAsync(
            new CrearTurnoRequest(medico.Id, paciente.Id, inicioUtc, DuracionMin: 30));

        // Assert
        var persistido = await db.Turnos.SingleAsync();
        Assert.Equal(turno.Id, persistido.Id);
        Assert.Equal(medico.Id, persistido.MedicoId);
        Assert.Equal(paciente.Id, persistido.PacienteId);
        Assert.Equal(inicioUtc, persistido.InicioUtc);
        Assert.Equal(inicioUtc.AddMinutes(30), persistido.FinUtc);
        Assert.Equal(EstadoTurno.Confirmado, persistido.Estado);
    }

    [Fact]
    public async Task CrearTurnoAsync_conMedicoInexistente_lanzaErrorYNoPersiste()
    {
        // Arrange
        using var db = NuevaDbEnMemoria();
        var service = new AgentTurnoService(db);
        var req = new CrearTurnoRequest(
            MedicoId: Guid.NewGuid(),   // no existe
            PacienteId: Guid.NewGuid(),
            InicioUtc: new DateTime(2030, 1, 10, 14, 0, 0, DateTimeKind.Utc),
            DuracionMin: 30);

        // Act + Assert
        await Assert.ThrowsAsync<MedicoNoEncontradoException>(() => service.CrearTurnoAsync(req));
        Assert.Equal(0, await db.Turnos.CountAsync());
    }

    [Fact]
    public async Task CrearTurnoAsync_conHorarioSolapado_lanzaErrorYNoPersiste()
    {
        // Arrange
        using var db = NuevaDbEnMemoria();
        var (medico, paciente) = await SembrarMedicoYPacienteAsync(db);

        var inicioExistente = new DateTime(2030, 1, 10, 14, 0, 0, DateTimeKind.Utc);
        db.Turnos.Add(new Turno
        {
            MedicoId = medico.Id,
            PacienteId = paciente.Id,
            InicioUtc = inicioExistente,
            FinUtc = inicioExistente.AddMinutes(30),
            Estado = EstadoTurno.Confirmado,
        });
        await db.SaveChangesAsync();

        var service = new AgentTurnoService(db);
        // 14:15–14:45 se solapa con el existente 14:00–14:30
        var req = new CrearTurnoRequest(medico.Id, paciente.Id, inicioExistente.AddMinutes(15), DuracionMin: 30);

        // Act + Assert
        await Assert.ThrowsAsync<HorarioOcupadoException>(() => service.CrearTurnoAsync(req));
        Assert.Equal(1, await db.Turnos.CountAsync()); // sigue solo el preexistente
    }
}
