using Microsoft.EntityFrameworkCore;
using Alfred2.Models; // Asumiendo que Cliente y Solicitud están aquí

namespace Alfred2.DBContext
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Solicitud> Solicitudes => Set<Solicitud>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<Usuario>().HasIndex(u => u.TelefonoBot).IsUnique();
            mb.Entity<Cliente>().HasIndex(c => new { c.UsuarioId, c.Telefono }).IsUnique();
            mb.Entity<Solicitud>().HasIndex(s => new { s.UsuarioId, s.Estado, s.Creado });

            base.OnModelCreating(mb);
        }
    }
}
