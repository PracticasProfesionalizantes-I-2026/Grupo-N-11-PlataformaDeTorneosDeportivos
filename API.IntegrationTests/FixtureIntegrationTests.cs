using System;
using System.Collections.Generic;
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
    public class FixtureIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public FixtureIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<Torneo> CrearTorneoEnDbAsync(EstadoTorneo estado, int cantidadEquiposConfirmados)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Test {Guid.NewGuid()}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                CostoInscripcion = 1000m,
                Estado = estado
            };

            db.Torneos.Add(torneo);

            for (int i = 0; i < cantidadEquiposConfirmados; i++)
            {
                var equipo = new Equipo
                {
                    Id = Guid.NewGuid(),
                    Nombre = $"Equipo {i + 1} - {Guid.NewGuid().ToString().Substring(0, 5)}",
                    EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                    TorneoId = torneo.Id
                };
                db.Equipos.Add(equipo);
            }

            await db.SaveChangesAsync();
            return torneo;
        }

        [Fact]
        public async Task GenerarFixture_Returns200OK()
        {
            // Arrange
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.Abierto, 4);
            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                Horarios = new List<string> { "14:00", "16:00" },
                Sedes = new List<string> { "Cancha Principal", "Cancha 2" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/generar-fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<GenerarFixtureResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(torneo.Id, result.TorneoId);
            Assert.Equal(EstadoTorneo.EnDesarrollo.ToString(), result.EstadoTorneo);
            Assert.True(result.TotalPartidosGenerados > 0);
            Assert.NotEmpty(result.Partidos);
        }

        [Fact]
        public async Task GenerarFixture_WithMissingParams_Returns400BadRequest()
        {
            // Arrange
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.Abierto, 4);
            var dtoInvalido = new GenerarFixtureRequestDTO
            {
                // Sin FechaInicio, sin Horarios, sin Sedes
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/generar-fixture", dtoInvalido);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GenerarFixture_WhenMinimumTeamsNotReached_Returns409Conflict()
        {
            // Arrange (Solo 2 equipos confirmados, mínimo reglamentario es 4)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.Abierto, 2);
            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "Cancha Principal" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/generar-fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task GenerarFixture_WhenTorneoNotOpen_Returns409Conflict()
        {
            // Arrange (Torneo en estado EnDesarrollo)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.EnDesarrollo, 4);
            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "Cancha Principal" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/generar-fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task GenerarFixture_WhenTorneoNotFound_Returns404NotFound()
        {
            // Arrange
            var nonExistentTorneoId = Guid.NewGuid();
            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "Cancha Principal" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{nonExistentTorneoId}/generar-fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetFixture_Returns200OK()
        {
            // Arrange
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.Abierto, 4);
            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                Horarios = new List<string> { "14:00", "16:00" },
                Sedes = new List<string> { "Cancha Principal", "Cancha 2" }
            };
            var genResponse = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/generar-fixture", dto);
            Assert.Equal(HttpStatusCode.OK, genResponse.StatusCode);

            // Act
            var response = await _client.GetAsync($"/api/torneos/{torneo.Id}/fixture");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<List<PartidoResponseDTO>>();
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.All(result, p => Assert.Equal(torneo.Id, p.TorneoId));
        }

        [Fact]
        public async Task GetFixture_WhenNotGenerated_Returns200OKAndEmpty()
        {
            // Arrange (Torneo recién creado sin partidos generados)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.Abierto, 2);

            // Act
            var response = await _client.GetAsync($"/api/torneos/{torneo.Id}/fixture");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<List<PartidoResponseDTO>>();
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFixture_WhenTorneoDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var response = await _client.GetAsync($"/api/torneos/{nonExistentId}/fixture");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
