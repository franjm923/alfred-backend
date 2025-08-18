using Microsoft.EntityFrameworkCore;
using Alfred2.Models; // Asumiendo que Cliente y Solicitud están aquí

namespace Alfred2.DBContext
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Solicitud> Solicitudes => Set<Solicitud>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<Cliente>().HasIndex(c => c.Telefono).IsUnique();
            mb.Entity<Solicitud>().HasIndex(s => new { s.ClienteId, s.Creado });

            base.OnModelCreating(mb);
        }
    }
}
