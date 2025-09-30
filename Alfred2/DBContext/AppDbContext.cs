using System;
using Microsoft.EntityFrameworkCore;
using Alfred2.Models; // <- donde pegaste las entidades (ajusta si cambiaste)

namespace Alfred2.DBContext
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSets nuevos (MVP médicos)
        public DbSet<Medico> Medicos => Set<Medico>();
        public DbSet<Paciente> Pacientes => Set<Paciente>();
        public DbSet<Servicio> Servicios => Set<Servicio>();
        public DbSet<Turno> Turnos => Set<Turno>();
        public DbSet<Conversacion> Conversaciones => Set<Conversacion>();
        public DbSet<Mensaje> Mensajes => Set<Mensaje>();
        public DbSet<DisponibilidadSemanal> Disponibilidades => Set<DisponibilidadSemanal>();
        public DbSet<BloqueoAgenda> Bloqueos => Set<BloqueoAgenda>();
        public DbSet<IntegracionCalendario> Integraciones => Set<IntegracionCalendario>();
        public DbSet<TurnoSyncCalendario> TurnoSincronizaciones => Set<TurnoSyncCalendario>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Precisión decimal
            modelBuilder.Entity<Servicio>().Property(p => p.Precio).HasPrecision(10, 2);
            modelBuilder.Entity<Turno>().Property(p => p.PrecioAcordado).HasPrecision(10, 2);

            // Relaciones y deletes
            modelBuilder.Entity<Paciente>()
                .HasOne(p => p.Medico)
                .WithMany(m => m.Pacientes)
                .HasForeignKey(p => p.MedicoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Servicio>()
                .HasOne(s => s.Medico)
                .WithMany(m => m.Servicios)
                .HasForeignKey(s => s.MedicoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Turno>()
                .HasOne(t => t.Medico)
                .WithMany(m => m.Turnos)
                .HasForeignKey(t => t.MedicoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Turno>()
                .HasOne(t => t.Paciente)
                .WithMany(p => p.Turnos)
                .HasForeignKey(t => t.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Turno>()
                .HasOne(t => t.Servicio)
                .WithMany(s => s.Turnos)
                .HasForeignKey(t => t.ServicioId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Conversacion>()
                .HasOne(c => c.Medico)
                .WithMany(m => m.Conversaciones)
                .HasForeignKey(c => c.MedicoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Conversacion>()
                .HasOne(c => c.Paciente)
                .WithMany(p => p.Conversaciones)
                .HasForeignKey(c => c.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Mensaje>()
                .HasOne(m => m.Conversacion)
                .WithMany(c => c.Mensajes)
                .HasForeignKey(m => m.ConversacionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DisponibilidadSemanal>()
                .HasOne(d => d.Medico)
                .WithMany(m => m.Disponibilidades)
                .HasForeignKey(d => d.MedicoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BloqueoAgenda>()
                .HasOne(b => b.Medico)
                .WithMany(m => m.Bloqueos)
                .HasForeignKey(b => b.MedicoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IntegracionCalendario>()
                .HasOne(i => i.Medico)
                .WithMany(m => m.Integraciones)
                .HasForeignKey(i => i.MedicoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TurnoSyncCalendario>()
                .HasOne(ts => ts.Turno)
                .WithMany(t => t.Sincronizaciones)
                .HasForeignKey(ts => ts.TurnoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TurnoSyncCalendario>()
                .HasOne(ts => ts.IntegracionCalendario)
                .WithMany(i => i.Sincronizaciones)
                .HasForeignKey(ts => ts.IntegracionCalendarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índices útiles
            modelBuilder.Entity<Medico>().HasIndex(m => m.Email).IsUnique();
            modelBuilder.Entity<Paciente>().HasIndex(p => new { p.MedicoId, p.TelefonoE164 }).IsUnique();
            modelBuilder.Entity<Servicio>().HasIndex(s => new { s.MedicoId, s.Nombre }).IsUnique();
            modelBuilder.Entity<Turno>().HasIndex(t => new { t.MedicoId, t.InicioUtc, t.Estado });
            modelBuilder.Entity<Conversacion>().HasIndex(c => new { c.MedicoId, c.PacienteId, c.Canal });
            modelBuilder.Entity<Mensaje>().HasIndex(m => new { m.ConversacionId, m.EnviadoUtc });
            modelBuilder.Entity<DisponibilidadSemanal>().HasIndex(d => new { d.MedicoId, d.DiaSemana });
            modelBuilder.Entity<BloqueoAgenda>().HasIndex(b => new { b.MedicoId, b.InicioUtc });
            modelBuilder.Entity<TurnoSyncCalendario>().HasIndex(ts => new { ts.TurnoId, ts.IntegracionCalendarioId }).IsUnique();
        }
    }
}
