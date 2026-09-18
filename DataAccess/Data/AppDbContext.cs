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
        }
    }
}
