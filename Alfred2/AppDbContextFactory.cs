using Alfred2.DBContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Alfred2;

/// <summary>
/// Factory de diseño para las herramientas de EF (migrations). Evita ejecutar Program.cs
/// (que corre MigrateAsync y levantaría el server) al generar/leer migraciones.
/// La cadena es un placeholder: `migrations add` no se conecta a la base.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=alfred;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
