using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    public class TorneosIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;

        public TorneosIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
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
            Assert.Equal("Abierto para Inscripción", created.Estado);
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
    }
}
