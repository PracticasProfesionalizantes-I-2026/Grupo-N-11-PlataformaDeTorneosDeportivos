using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataAccess.Entities;

namespace DataAccess.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Torneo> Torneos { get; set; } = null!;
        public DbSet<Equipo> Equipos { get; set; } = null!;
        public DbSet<Jugador> Jugadores { get; set; } = null!;
        public DbSet<Usuario> Usuarios { get; set; } = null!;
        public DbSet<Partido> Partidos { get; set; } = null!;
        public DbSet<Incidencia> Incidencias { get; set; } = null!;
        public DbSet<Publicidad> Publicidades { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure DeleteBehavior.Restrict on relationships
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }

            modelBuilder.Entity<Torneo>()
                .Property(t => t.CostoInscripcion)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Torneo>()
                .Property(t => t.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();

            modelBuilder.Entity<Torneo>()
                .HasOne(t => t.Campeon)
                .WithMany()
                .HasForeignKey(t => t.CampeonId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Torneo>()
                .HasMany(t => t.Equipos)
                .WithOne(e => e.Torneo)
                .HasForeignKey(e => e.TorneoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Equipo>()
                .Property(e => e.ComprobanteUrl)
                .HasMaxLength(500);

            modelBuilder.Entity<Equipo>()
                .HasOne(e => e.Capitan)
                .WithMany()
                .HasForeignKey(e => e.CapitanId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Dni)
                .IsUnique();

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }

        public override int SaveChanges()
        {
            UpdateRowVersions();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            UpdateRowVersions();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateRowVersions();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            UpdateRowVersions();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void UpdateRowVersions()
        {
            var entries = ChangeTracker.Entries<Torneo>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.RowVersion == null || entry.Entity.RowVersion.Length == 0)
                    {
                        entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                }
            }
        }
    }
}
