using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
    }
}
