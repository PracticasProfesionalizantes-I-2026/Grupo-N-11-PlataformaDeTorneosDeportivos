using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Services;
using DataAccess.Data;
using DataAccess.Entities;
using DataAccess.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    /// <summary>
    /// Pruebas dedicadas a validar el Comportamiento Transaccional (ACID / Unit of Work)
    /// garantizando que las operaciones compuestas (CU-06 y CU-08/16) mantengan atomicidad,
    /// ejecuten Commit en éxito y Rollback total ante fallos intermedios sobre SQLite real.
    /// </summary>
    public class TransaccionesAcidIntegrationTests
    {
        private static async Task<(SqliteConnection connection, AppDbContext context, UnitOfWork uow)> CreateSqliteContextAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var uow = new UnitOfWork(context);
            return (connection, context, uow);
        }

        [Fact]
        public async Task UnitOfWork_ConSqliteReal_RollbackRevierteCambiosAtómicamente()
        {
            var (connection, context, uow) = await CreateSqliteContextAsync();
            await using (connection)
            await using (context)
            await using (uow)
            {
                var torneoId = Guid.NewGuid();

                // Act: Iniciar transacción, agregar registro y guardar en el contexto
                await uow.BeginTransactionAsync();

                context.Torneos.Add(new Torneo
                {
                    Id = torneoId,
                    Nombre = "Torneo Transaccional Rollback",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                });
                await context.SaveChangesAsync();

                // Verificamos que dentro de la transacción activa existe
                Assert.Equal(1, await context.Torneos.CountAsync());

                // Rollback explícito
                await uow.RollbackTransactionAsync();

                // Limpiamos ChangeTracker para consultar el estado real persistido en SQLite
                context.ChangeTracker.Clear();

                // Assert: La base de datos no debe contener ningún registro
                var countFinal = await context.Torneos.CountAsync();
                Assert.Equal(0, countFinal);
            }
        }

        [Fact]
        public async Task UnitOfWork_ConSqliteReal_CommitPersisteCambiosAtómicamente()
        {
            var (connection, context, uow) = await CreateSqliteContextAsync();
            await using (connection)
            await using (context)
            await using (uow)
            {
                var torneoId = Guid.NewGuid();

                // Act: Iniciar transacción, agregar y hacer Commit
                await uow.BeginTransactionAsync();

                context.Torneos.Add(new Torneo
                {
                    Id = torneoId,
                    Nombre = "Torneo Transaccional Commit",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                });
                await context.SaveChangesAsync();
                await uow.CommitTransactionAsync();

                // Limpiamos ChangeTracker
                context.ChangeTracker.Clear();

                // Assert: El registro debe estar efectivamente persistido en SQLite
                var torneoGuardado = await context.Torneos.FindAsync(torneoId);
                Assert.NotNull(torneoGuardado);
                Assert.Equal("Torneo Transaccional Commit", torneoGuardado.Nombre);
            }
        }

        [Fact]
        public async Task FixtureService_ConSqliteReal_FalloIntermedio_EjecutaRollbackYNoDejaPartidosHuerfanos()
        {
            var (connection, context, uow) = await CreateSqliteContextAsync();
            await using (connection)
            await using (context)
            await using (uow)
            {
                var torneoId = Guid.NewGuid();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = "Torneo Liga ACID",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 4,
                    Estado = EstadoTorneo.InscripcionesCerradas
                };

                var equipos = new List<Equipo>
                {
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo 1", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo 2", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo 3", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo 4", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado }
                };

                context.Torneos.Add(torneo);
                context.Equipos.AddRange(equipos);
                await context.SaveChangesAsync();

                // Repositorio con fallo simulado en la inserción de partidos
                var torneoRepo = new TorneoRepository(context);
                var partidoRepoFailing = new FailingPartidoRepository(context);

                var fixtureService = new FixtureService(torneoRepo, partidoRepoFailing, uow);

                var dto = new GenerarFixtureRequestDTO
                {
                    FechaInicio = DateTime.Today.AddDays(1),
                    Horarios = new List<string> { "15:00", "17:00" },
                    Sedes = new List<string> { "Estadio Principal" }
                };

                // Act & Assert: Debe fallar con la excepción inducida
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    fixtureService.GenerarFixtureAsync(torneoId, dto));

                // Limpiamos ChangeTracker para verificar SQLite real
                context.ChangeTracker.Clear();

                // Assert ACID:
                // 1. No debe existir ningún partido huérfano guardado en SQLite
                var partidosGuardados = await context.Partidos.Where(p => p.TorneoId == torneoId).CountAsync();
                Assert.Equal(0, partidosGuardados);

                // 2. El torneo debe mantener su estado 'InscripcionesCerradas' original (no 'EnDesarrollo')
                var torneoRecuperado = await context.Torneos.FindAsync(torneoId);
                Assert.NotNull(torneoRecuperado);
                Assert.Equal(EstadoTorneo.InscripcionesCerradas, torneoRecuperado.Estado);
            }
        }

        [Fact]
        public async Task InscripcionService_ConSqliteReal_FalloIntermedio_EjecutaRollbackYNoAlteraEstadoEquipo()
        {
            var (connection, context, uow) = await CreateSqliteContextAsync();
            await using (connection)
            await using (context)
            await using (uow)
            {
                var torneoId = Guid.NewGuid();
                var equipoId = Guid.NewGuid();

                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = "Torneo Cupo ACID",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8,
                    EsPago = false,
                    CostoInscripcion = 0,
                    Estado = EstadoTorneo.Abierto
                };

                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = "Equipo Pendiente ACID",
                    TorneoId = torneoId,
                    EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                    EdicionListaHabilitada = false,
                    PagoValidado = true
                };

                context.Torneos.Add(torneo);
                context.Equipos.Add(equipo);
                await context.SaveChangesAsync();

                // Repositorio con fallo inducido al actualizar el equipo tras actualizar el torneo
                var torneoRepo = new TorneoRepository(context);
                var equipoRepoFailing = new FailingEquipoRepository(context);

                var inscripcionService = new InscripcionService(torneoRepo, equipoRepoFailing, uow);

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    inscripcionService.AprobarInscripcionAsync(torneoId, equipoId));

                // Limpiamos ChangeTracker para verificar SQLite
                context.ChangeTracker.Clear();

                // Assert ACID:
                // El equipo NO debe haber quedado como 'InscritoYConfirmado'; debe conservar 'PendienteDeValidacion'
                var equipoRecuperado = await context.Equipos.FindAsync(equipoId);
                Assert.NotNull(equipoRecuperado);
                Assert.Equal(EstadoInscripcion.PendienteDeValidacion, equipoRecuperado.EstadoInscripcion);
            }
        }

        #region Clases Helper para simular fallos inducidos en SQLite

        private class FailingPartidoRepository : PartidoRepository
        {
            public FailingPartidoRepository(AppDbContext context) : base(context) { }

            public override Task AddRangeAsync(IEnumerable<Partido> entities)
            {
                throw new InvalidOperationException("Fallo inducido al persistir el lote de partidos generados.");
            }
        }

        private class FailingEquipoRepository : EquipoRepository
        {
            public FailingEquipoRepository(AppDbContext context) : base(context) { }

            public override Task UpdateAsync(Equipo entity)
            {
                throw new InvalidOperationException("Fallo inducido al actualizar el estado del equipo.");
            }
        }

        #endregion
    }
}

