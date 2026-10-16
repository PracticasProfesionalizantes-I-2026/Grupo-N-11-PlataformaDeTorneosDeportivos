using System;
using System.Collections.Generic;
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
    public class FixtureAutomaticoIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public FixtureAutomaticoIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<Torneo> CrearTorneoEnDbAsync(EstadoTorneo estado, int cantidadEquiposConfirmados, Modalidad modalidad = Modalidad.Liga)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo CU16 {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = modalidad,
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
                    Nombre = $"Equipo CU16 {i + 1} - {Guid.NewGuid().ToString().Substring(0, 5)}",
                    EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                    TorneoId = torneo.Id
                };
                db.Equipos.Add(equipo);
            }

            await db.SaveChangesAsync();
            return torneo;
        }

        [Fact]
        public async Task GenerarFixture_WithValidData_Returns201Created()
        {
            // Arrange (CU-16 Flujo Principal HTTP - 201 Created)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.InscripcionesCerradas, 4);
            var dto = new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                FranjasHorarias = new List<string> { "14:00", "16:00" },
                CanchasDisponibles = new List<string> { "Cancha Principal", "Cancha 2" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            Assert.Contains($"/api/torneos/{torneo.Id}/fixture", response.Headers.Location.ToString());

            var result = await response.Content.ReadFromJsonAsync<FixtureResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(torneo.Id, result.TorneoId);
            Assert.Equal(EstadoTorneo.EnDesarrollo.ToString(), result.EstadoTorneo);
            Assert.True(result.CantidadPartidosProgramados > 0);
            Assert.NotEmpty(result.Partidos);
        }

        [Fact]
        public async Task GenerarFixture_WithMalformedJson_Returns400BadRequest()
        {
            // Arrange (CU-16 Flujo 1a HTTP - JSON malformado)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.InscripcionesCerradas, 4);
            var content = new StringContent("{ \"fechaInicio\": \"invalid-datetime-string\", \"canchasDisponibles\": [ }", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"/api/torneos/{torneo.Id}/fixture", content);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GenerarFixture_WithPastStartDate_Returns400BadRequest()
        {
            // Arrange (CU-16 Flujo 2a HTTP - Fecha en el pasado)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.InscripcionesCerradas, 4);
            var dto = new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(-5),
                FranjasHorarias = new List<string> { "14:00" },
                CanchasDisponibles = new List<string> { "Cancha Principal" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GenerarFixture_WhenTorneoNotFound_Returns404NotFound()
        {
            // Arrange (CU-16 Flujo 3a HTTP - Torneo inexistente)
            var nonExistentId = Guid.NewGuid();
            var dto = new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                FranjasHorarias = new List<string> { "14:00" },
                CanchasDisponibles = new List<string> { "Cancha Principal" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{nonExistentId}/fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GenerarFixture_WhenTorneoNotClosed_Returns409Conflict()
        {
            // Arrange (CU-16 Flujo 3b HTTP - Torneo aún en estado Abierto)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.Abierto, 4);
            var dto = new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                FranjasHorarias = new List<string> { "14:00", "16:00" },
                CanchasDisponibles = new List<string> { "Cancha 1", "Cancha 2" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task GenerarFixture_WhenTeamsBelowFour_Returns422UnprocessableEntity()
        {
            // Arrange (CU-16 Flujo 3c HTTP - Menos de 4 equipos confirmados)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.InscripcionesCerradas, 2);
            var dto = new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                FranjasHorarias = new List<string> { "14:00" },
                CanchasDisponibles = new List<string> { "Cancha 1" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task GenerarFixture_WhenOddTeams_Returns201WithByes()
        {
            // Arrange (CU-16 Flujo 3d HTTP - Equipos impares en Liga)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.InscripcionesCerradas, 5, Modalidad.Liga);
            var dto = new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                FranjasHorarias = new List<string> { "14:00", "16:00", "18:00" },
                CanchasDisponibles = new List<string> { "Cancha 1", "Cancha 2" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<FixtureResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(5, result.TotalJornadas);
            Assert.Equal(5, result.Descansos.Count);
        }

        [Fact]
        public async Task GenerarFixture_WhenNonPowerOfTwo_Returns201WithBrackets()
        {
            // Arrange (CU-16 Flujo 3e HTTP - No potencia de 2 en Eliminación Directa)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.InscripcionesCerradas, 5, Modalidad.EliminacionDirecta);
            var dto = new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                FranjasHorarias = new List<string> { "14:00" },
                CanchasDisponibles = new List<string> { "Servidor 1" }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<FixtureResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(3, result.PasesDirectos.Count); // 8 - 5 = 3 Byes
            Assert.Equal(1, result.CantidadPartidosProgramados);
        }

        [Fact]
        public async Task GenerarFixture_WhenInsufficientSlots_Returns422UnprocessableEntity()
        {
            // Arrange (CU-16 Flujo 3f HTTP - Disponibilidad horaria insuficiente RN-09)
            var torneo = await CrearTorneoEnDbAsync(EstadoTorneo.InscripcionesCerradas, 4, Modalidad.Liga);
            var dto = new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                FranjasHorarias = new List<string> { "14:00" },
                CanchasDisponibles = new List<string> { "Cancha 1" } // 1 * 1 = 1 slot, pero se necesitan 2 por jornada
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/fixture", dto);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
    }
}
