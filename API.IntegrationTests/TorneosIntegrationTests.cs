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
    public class TorneosIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public TorneosIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetTorneos_ReturnsOkResponse()
        {
            // Act
            var response = await _client.GetAsync("/api/torneos");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WithNegativeCost_ReturnsBadRequest()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Test Integracion",
                Disciplina = Disciplina.ESports,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                CostoInscripcion = -100 // Invalid
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WithValidPayload_Returns201Created()
        {
            // Arrange
            var uniqueName = $"Torneo Integracion {Guid.NewGuid():N}";
            var dto = new TorneoCreateDTO
            {
                Nombre = uniqueName,
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 1500m
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);

            var created = await response.Content.ReadFromJsonAsync<TorneoResponseDTO>();
            Assert.NotNull(created);
            Assert.Equal(uniqueName, created.Nombre);
            Assert.Equal("Futbol", created.Disciplina);
            Assert.Equal("EliminacionDirecta", created.Modalidad);
            Assert.Equal(8, created.CupoMaximo);
            Assert.Equal(EstadoTorneo.Abierto.ToString(), created.Estado);
        }

        [Fact]
        public async Task CreateTorneo_WithMalformedJson_Returns400BadRequest()
        {
            // Arrange - JSON sintácticamente corrupto
            var corruptContent = new StringContent("{ \"nombre\": \"Torneo Corrupto\", ", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/torneos", corruptContent);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WithMissingRequiredFields_Returns400BadRequest()
        {
            // Arrange - Falta el nombre requerido
            var dto = new TorneoCreateDTO
            {
                Nombre = "", // Invalido
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WhenDisciplinaNotFound_Returns404NotFound()
        {
            // Arrange - Disciplina inválida / inexistente
            var dto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Disciplina {Guid.NewGuid():N}",
                DisciplinaId = 999, // Inexistente
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WhenDuplicateName_Returns409Conflict()
        {
            // Arrange - Crear primer torneo
            var duplicateName = $"Torneo Unico {Guid.NewGuid():N}";
            var dto = new TorneoCreateDTO
            {
                Nombre = duplicateName,
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };

            var firstResponse = await _client.PostAsJsonAsync("/api/torneos", dto);
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

            // Act - Intentar crear otro con el mismo nombre
            var secondResponse = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WhenEliminacionCupoInvalid_Returns422UnprocessableEntity()
        {
            // Arrange - RN-04: Eliminación directa con cupo 6 (no es potencia de 2)
            var dto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Brackets Invalid {Guid.NewGuid():N}",
                Disciplina = Disciplina.ESports,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 6 // Inválido
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WhenPagoWithInvalidCosto_Returns400BadRequest()
        {
            // Arrange - RN-05: Torneo pago con costo 0
            var dto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Pago Sin Costo {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 0m
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WhenCuentaRecaudadoraInactive_Returns422UnprocessableEntity()
        {
            // Arrange - RN-05: Torneo pago con cuenta recaudadora inactiva
            var dto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Pago Inactivo {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 1000m,
                CuentaRecaudadoraActiva = false // Cuenta inactiva
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task GetPosiciones_Returns200OK()
        {
            // Arrange (CU-13 Flujo Principal HTTP)
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Liga Posiciones {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                Estado = EstadoTorneo.EnDesarrollo
            };
            db.Torneos.Add(torneo);

            var eq1 = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo 1 {Guid.NewGuid():N}",
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                TorneoId = torneo.Id
            };
            var eq2 = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo 2 {Guid.NewGuid():N}",
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                TorneoId = torneo.Id
            };
            db.Equipos.AddRange(eq1, eq2);

            var partido = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneo.Id,
                EquipoLocalId = eq1.Id,
                EquipoVisitanteId = eq2.Id,
                MarcadorLocal = 2,
                MarcadorVisitante = 0,
                Estado = EstadoPartido.Finalizado,
                FechaHora = DateTime.UtcNow,
                Sede = "Cancha 1",
                Ronda = 1
            };
            db.Partidos.Add(partido);
            await db.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/torneos/{torneo.Id}/posiciones");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var posiciones = await response.Content.ReadFromJsonAsync<List<PosicionTablaDTO>>();
            Assert.NotNull(posiciones);
            Assert.NotEmpty(posiciones);
            Assert.Equal(eq1.Id, posiciones[0].EquipoId);
            Assert.Equal(3, posiciones[0].Puntos);
        }

        [Fact]
        public async Task GetPosiciones_WhenTorneoIsElimination_Returns400BadRequest()
        {
            // Arrange (CU-13 Flujo 3a HTTP)
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Brackets Posiciones {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 8,
                Estado = EstadoTorneo.EnDesarrollo
            };
            db.Torneos.Add(torneo);
            await db.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/torneos/{torneo.Id}/posiciones");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetPosiciones_WhenTorneoDoesNotExist_Returns404NotFound()
        {
            // Act (CU-13 Flujo 3b HTTP)
            var response = await _client.GetAsync($"/api/torneos/{Guid.NewGuid()}/posiciones");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetLlaves_Returns200OK()
        {
            // Arrange (CU-13 Flujo Principal Brackets HTTP)
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Llaves {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 8,
                Estado = EstadoTorneo.EnDesarrollo
            };
            db.Torneos.Add(torneo);

            var eq1 = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo Llaves 1 {Guid.NewGuid():N}",
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                TorneoId = torneo.Id
            };
            var eq2 = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo Llaves 2 {Guid.NewGuid():N}",
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                TorneoId = torneo.Id
            };
            db.Equipos.AddRange(eq1, eq2);

            var partido = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneo.Id,
                EquipoLocalId = eq1.Id,
                EquipoVisitanteId = eq2.Id,
                MarcadorLocal = 1,
                MarcadorVisitante = 0,
                Estado = EstadoPartido.Finalizado,
                FechaHora = DateTime.UtcNow,
                Sede = "Cancha 1",
                Ronda = 1
            };
            db.Partidos.Add(partido);
            await db.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/torneos/{torneo.Id}/llaves");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var bracket = await response.Content.ReadFromJsonAsync<BracketTorneoDTO>();
            Assert.NotNull(bracket);
            Assert.Equal(torneo.Id, bracket.TorneoId);
            Assert.NotEmpty(bracket.Rondas);
        }

        [Fact]
        public async Task GetLlaves_WhenTorneoIsLiga_Returns400BadRequest()
        {
            // Arrange (CU-13 Modalidad Incompatible Brackets HTTP)
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Liga Llaves {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                Estado = EstadoTorneo.EnDesarrollo
            };
            db.Torneos.Add(torneo);
            await db.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/torneos/{torneo.Id}/llaves");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetLlaves_WhenTorneoDoesNotExist_Returns404NotFound()
        {
            // Act (CU-13 Torneo Inexistente Brackets HTTP)
            var response = await _client.GetAsync($"/api/torneos/{Guid.NewGuid()}/llaves");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
