using Alfred2.DBContext;
using Alfred2.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alfred2.Tests;

public class AdminMedicoServiceTests
{
    private static AppDbContext NuevaDbEnMemoria() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task RegistrarMedicoDePrueba_conIdYNumero_creaYPersisteElMedico()
    {
        // Arrange
        using var db = NuevaDbEnMemoria();
        var service = new AdminMedicoService(db);
        var id = Guid.NewGuid();

        // Act
        var medico = await service.RegistrarMedicoDePruebaAsync(
            new RegistrarMedicoPruebaRequest(id, "5491100000000", "Dr. Test"));

        // Assert
        Assert.Equal(id, medico.Id);
        Assert.Equal("5491100000000", medico.TelefonoE164);
        Assert.Equal("Dr. Test", medico.NombreCompleto);
        var persistido = await db.Medicos.SingleAsync();
        Assert.Equal(id, persistido.Id);
    }

    [Fact]
    public async Task RegistrarMedicoDePrueba_conMismoNumero_actualizaSinDuplicar()
    {
        // Arrange
        using var db = NuevaDbEnMemoria();
        var service = new AdminMedicoService(db);

        // Act
        await service.RegistrarMedicoDePruebaAsync(new RegistrarMedicoPruebaRequest(null, "5491100000000", "Dr. Uno"));
        await service.RegistrarMedicoDePruebaAsync(new RegistrarMedicoPruebaRequest(null, "5491100000000", "Dr. Dos"));

        // Assert
        var medico = await db.Medicos.SingleAsync(); // no se duplica
        Assert.Equal("Dr. Dos", medico.NombreCompleto);
    }
}
