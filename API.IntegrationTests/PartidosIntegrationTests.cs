using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DataAccess.Data;
using DataAccess.Entities;
using Microsoft.Extensions.DependencyInjection;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    public class PartidosIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly CustomWebApplicationFactory<API.Program> _factory;
        private readonly HttpClient _client;

        public PartidosIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task UpdateResultado_Returns200OK()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();
            var localId = Guid.NewGuid();
            var visitanteId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = "Torneo Liga 2026",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8,
                    CostoInscripcion = 0
                };

                var local = new Equipo { Id = localId, Nombre = "Club Local", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
                var visitante = new Equipo { Id = visitanteId, Nombre = "Club Visitante", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };

                var partido = new Partido
                {
                    Id = partidoId,
                    TorneoId = torneoId,
                    EquipoLocalId = localId,
                    EquipoVisitanteId = visitanteId,
                    FechaHora = DateTime.Today.AddHours(18),
                    Sede = "Cancha Principal",
                    Ronda = 1,
                    Estado = EstadoPartido.Pendiente
                };

                db.Torneos.Add(torneo);
                db.Equipos.AddRange(local, visitante);
                db.Partidos.Add(partido);
                await db.SaveChangesAsync();
            }

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 3,
                MarcadorVisitante = 1
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/partidos/{partidoId}/resultado", dto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<PartidoResultadoResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal("Finalizado", result.Estado);
            Assert.Equal(3, result.MarcadorLocal);
            Assert.Equal(1, result.MarcadorVisitante);
            Assert.Equal(localId, result.GanadorId);
        }

        [Fact]
        public async Task UpdateResultado_WithNegativeScores_Returns400BadRequest()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = -1, // Inválido
                MarcadorVisitante = 2
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/partidos/{partidoId}/resultado", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateResultado_WhenPartidoClosed_Returns409Conflict()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.Torneos.Add(new Torneo
                {
                    Id = torneoId,
                    Nombre = "Torneo Finalizado Test",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8,
                    CostoInscripcion = 0
                });

                db.Partidos.Add(new Partido
                {
                    Id = partidoId,
                    TorneoId = torneoId,
                    FechaHora = DateTime.Today.AddHours(10),
                    Sede = "Sede A",
                    Ronda = 1,
                    Estado = EstadoPartido.Finalizado // Ya finalizado
                });

                await db.SaveChangesAsync();
            }

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 1,
                MarcadorVisitante = 0
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/partidos/{partidoId}/resultado", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task UpdateResultado_WhenDrawInElimination_Returns409Conflict()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.Torneos.Add(new Torneo
                {
                    Id = torneoId,
                    Nombre = "Torneo Playoffs",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.EliminacionDirecta,
                    CupoMaximo = 4,
                    CostoInscripcion = 0
                });

                db.Partidos.Add(new Partido
                {
                    Id = partidoId,
                    TorneoId = torneoId,
                    FechaHora = DateTime.Today.AddHours(14),
                    Sede = "Sede Playoffs",
                    Ronda = 1,
                    Estado = EstadoPartido.Pendiente
                });

                await db.SaveChangesAsync();
            }

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 2,
                MarcadorVisitante = 2 // Empate sin penales
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/partidos/{partidoId}/resultado", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }
}
