using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using DataAccess.Data;
using DataAccess.Entities;
using Microsoft.Extensions.DependencyInjection;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    public class InscripcionesIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly CustomWebApplicationFactory<API.Program> _factory;
        private readonly HttpClient _client;

        public InscripcionesIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(Torneo torneo, Equipo equipo)> CreateTestTorneoAndEquipoAsync(
            string nombreTorneo,
            int cupoMaximo,
            bool esPago,
            decimal costo,
            EstadoInscripcion estadoEquipo = EstadoInscripcion.PendienteDeValidacion,
            bool pagoValidado = true)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = nombreTorneo,
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = cupoMaximo,
                EsPago = esPago,
                CostoInscripcion = costo,
                Estado = EstadoTorneo.Abierto
            };
            db.Torneos.Add(torneo);

            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = estadoEquipo,
                PagoValidado = pagoValidado
            };
            db.Equipos.Add(equipo);

            await db.SaveChangesAsync();
            return (torneo, equipo);
        }

        [Fact]
        public async Task AprobarInscripcion_WithValidTeam_Returns200Ok()
        {
            // Arrange
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Aprobar {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: false,
                costo: 0m);

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<AprobarInscripcionResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(equipo.Id, result.EquipoId);
            Assert.Equal(EstadoInscripcion.InscritoYConfirmado.ToString(), result.EstadoInscripcion);
            Assert.Equal(7, result.CuposDisponibles); // 8 - 1 = 7
        }

        [Fact]
        public async Task AprobarInscripcion_WhenLastSpotConfirmed_TransitionsTournamentToInscripcionesCerradas()
        {
            // Arrange - Torneo con CupoMaximo = 1 y 1 equipo pendiente
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Cierre Cupo {Guid.NewGuid():N}",
                cupoMaximo: 1,
                esPago: false,
                costo: 0m);

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<AprobarInscripcionResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(0, result.CuposDisponibles);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var torneoDb = await db.Torneos.FindAsync(torneo.Id);
            Assert.NotNull(torneoDb);
            Assert.Equal(EstadoTorneo.InscripcionesCerradas, torneoDb.Estado);
        }

        [Fact]
        public async Task AprobarInscripcion_WithMalformedIds_Returns400BadRequest()
        {
            // Arrange - IDs malformados (no son GUIDs)
            var response = await _client.PatchAsync(
                "/api/torneos/id-invalido/equipos/otro-invalido/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task AprobarInscripcion_WhenNotFound_Returns404NotFound()
        {
            // Arrange - GUIDs inexistentes
            var nonExistentTorneoId = Guid.NewGuid();
            var nonExistentEquipoId = Guid.NewGuid();

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{nonExistentTorneoId}/equipos/{nonExistentEquipoId}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task AprobarInscripcion_WhenTeamAlreadyProcessed_Returns409Conflict()
        {
            // Arrange - Equipo ya procesado (InscritoYConfirmado)
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Ya Procesado {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: false,
                costo: 0m,
                estadoEquipo: EstadoInscripcion.InscritoYConfirmado);

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task AprobarInscripcion_WhenTournamentFull_Returns409Conflict()
        {
            // Arrange - RN-06: Torneo con cupo máximo lleno (Cupo = 1, y ya hay 1 confirmado)
            var (torneo, equipoPendiente) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Lleno {Guid.NewGuid():N}",
                cupoMaximo: 1,
                esPago: false,
                costo: 0m,
                estadoEquipo: EstadoInscripcion.PendienteDeValidacion);

            // Crear un equipo ya confirmado en el mismo torneo
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Equipos.Add(new Equipo
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Equipo Ya Confirmado",
                    TorneoId = torneo.Id,
                    EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
                });
                await db.SaveChangesAsync();
            }

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipoPendiente.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task AprobarInscripcion_WhenUnpaidTournament_Returns422UnprocessableEntity()
        {
            // Arrange - RN-06: Torneo pago pero equipo sin comprobante de pago validado
            var (torneo, equipoSinPago) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Pago {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: true,
                costo: 1500m,
                estadoEquipo: EstadoInscripcion.PendienteDeValidacion,
                pagoValidado: false);

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipoSinPago.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task RechazarInscripcion_WithValidReason_Returns200Ok()
        {
            // Arrange - RN-03: Rechazo válido con motivo
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Rechazar {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: false,
                costo: 0m);

            var dto = new RechazarInscripcionDTO
            {
                MotivoRechazo = "La nómina presenta DNIs ilegibles en las credenciales."
            };

            // Act
            var response = await _client.PatchAsJsonAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/rechazar",
                dto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<RechazarInscripcionResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(equipo.Id, result.EquipoId);
            Assert.Equal(EstadoInscripcion.RechazadoObservado.ToString(), result.EstadoInscripcion);
            Assert.Equal(dto.MotivoRechazo, result.MotivoRechazo);
            Assert.True(result.EdicionListaHabilitada); // RN-03: edición desbloqueada
        }

        [Fact]
        public async Task RechazarInscripcion_WithEmptyReason_Returns400BadRequest()
        {
            // Arrange - Rechazo sin motivo (violación de [Required])
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Rechazar Vacio {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: false,
                costo: 0m);

            var dto = new RechazarInscripcionDTO
            {
                MotivoRechazo = "" // Inválido
            };

            // Act
            var response = await _client.PatchAsJsonAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/rechazar",
                dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
