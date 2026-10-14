using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
        public async Task CreateTorneo_WithValidPayload_Returns201Created()
        {
            // Arrange
            var payload = new
            {
                nombre = $"Torneo Integracion Exitoso {Guid.NewGuid()}",
                disciplinaId = 0,
                modalidad = 0,
                cupoMaximo = 16,
                esPago = false,
                costoInscripcion = 0
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);

            var createdTorneo = await response.Content.ReadFromJsonAsync<TorneoResponseDTO>();
            Assert.NotNull(createdTorneo);
            Assert.Equal(payload.nombre, createdTorneo.Nombre);
            Assert.Equal("Abierto para Inscripción", createdTorneo.Estado);
        }

        [Fact]
        public async Task CreateTorneo_WithMalformedJson_Returns400BadRequest()
        {
            // Arrange
            var malformedContent = new StringContent("{ \"nombre\": \"Incompleto\", ", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/torneos", malformedContent);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WithMissingRequiredFields_Returns400BadRequest()
        {
            // Arrange (sin nombre ni modalidad)
            var payload = new
            {
                cupoMaximo = 8
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WhenDisciplinaNotFound_Returns404NotFound()
        {
            // Arrange
            var payload = new
            {
                nombre = $"Torneo Sin Disciplina {Guid.NewGuid()}",
                disciplinaId = 999, // Disciplina inexistente
                modalidad = 0,
                cupoMaximo = 8,
                esPago = false,
                costoInscripcion = 0
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WhenDuplicateName_Returns409Conflict()
        {
            // Arrange
            var nombreDuplicado = $"Torneo Duplicado {Guid.NewGuid()}";
            var payload = new
            {
                nombre = nombreDuplicado,
                disciplinaId = 0,
                modalidad = 0,
                cupoMaximo = 8,
                esPago = false,
                costoInscripcion = 0
            };

            // Primer alta exitosa
            var firstResponse = await _client.PostAsJsonAsync("/api/torneos", payload);
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

            // Act: segundo intento con el mismo nombre
            var secondResponse = await _client.PostAsJsonAsync("/api/torneos", payload);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WhenEliminacionCupoInvalid_Returns422UnprocessableEntity()
        {
            // Arrange (EliminacionDirecta = 1, pero cupo = 6 no es potencia de 2)
            var payload = new
            {
                nombre = $"Torneo Eliminacion Invalido {Guid.NewGuid()}",
                disciplinaId = 0,
                modalidad = 1,
                cupoMaximo = 6,
                esPago = false,
                costoInscripcion = 0
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task CreateTorneo_WhenPagoWithInvalidCosto_Returns400BadRequest()
        {
            // Arrange (esPago = true pero costo = 0)
            var payload = new
            {
                nombre = $"Torneo Pago Invalido {Guid.NewGuid()}",
                disciplinaId = 0,
                modalidad = 0,
                cupoMaximo = 8,
                esPago = true,
                costoInscripcion = 0
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
