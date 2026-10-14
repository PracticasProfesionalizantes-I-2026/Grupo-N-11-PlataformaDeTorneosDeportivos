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
    public class VeedoresIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly CustomWebApplicationFactory<API.Program> _factory;
        private readonly HttpClient _client;

        public VeedoresIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateVeedor_WithValidData_Returns201Created()
        {
            // Arrange
            var uniqueDni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8);
            var uniqueEmail = $"veedor_{Guid.NewGuid():N}@torneos.com";

            var dto = new VeedorCreateDTO
            {
                Nombre = "Veedor Integracion",
                Apellido = "Oficial",
                Dni = uniqueDni,
                Email = uniqueEmail,
                Telefono = "123456789",
                SedeAsignada = "Sede Central"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/veedores", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);

            var created = await response.Content.ReadFromJsonAsync<VeedorResponseDTO>();
            Assert.NotNull(created);
            Assert.Equal(dto.Nombre, created.Nombre);
            Assert.Equal(dto.Email, created.Email);
            Assert.Equal(RolUsuario.Veedor.ToString(), created.Rol);
            Assert.True(created.Activo);
        }

        [Fact]
        public async Task CreateVeedor_WithMissingData_Returns400BadRequest()
        {
            // Arrange - Faltan campos requeridos (nombre y email vacíos)
            var dto = new VeedorCreateDTO
            {
                Nombre = "",
                Apellido = "Incompleto",
                Dni = "",
                Email = "email-invalido"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/veedores", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateVeedor_WhenDuplicateData_Returns409Conflict()
        {
            // Arrange - Crear primer veedor
            var dniCompartido = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8);
            var emailCompartido = $"duplicado_{Guid.NewGuid():N}@torneos.com";

            var dtoPrimero = new VeedorCreateDTO
            {
                Nombre = "Primero",
                Apellido = "Veedor",
                Dni = dniCompartido,
                Email = emailCompartido
            };

            var primerResponse = await _client.PostAsJsonAsync("/api/veedores", dtoPrimero);
            Assert.Equal(HttpStatusCode.Created, primerResponse.StatusCode);

            // Act - Intentar crear otro con el mismo DNI o Email
            var dtoSegundo = new VeedorCreateDTO
            {
                Nombre = "Segundo",
                Apellido = "Veedor",
                Dni = dniCompartido,
                Email = $"otro_{Guid.NewGuid():N}@torneos.com"
            };

            var segundoResponse = await _client.PostAsJsonAsync("/api/veedores", dtoSegundo);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, segundoResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateVeedor_Returns200OK()
        {
            // Arrange - Crear veedor inicial
            var uniqueDni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8);
            var uniqueEmail = $"update_{Guid.NewGuid():N}@torneos.com";

            var dto = new VeedorCreateDTO
            {
                Nombre = "Antes de Update",
                Apellido = "Viejo",
                Dni = uniqueDni,
                Email = uniqueEmail
            };

            var createResponse = await _client.PostAsJsonAsync("/api/veedores", dto);
            var created = await createResponse.Content.ReadFromJsonAsync<VeedorResponseDTO>();
            Assert.NotNull(created);

            var updateDto = new VeedorUpdateDTO
            {
                Nombre = "Despues de Update",
                Apellido = "Modificado",
                Telefono = "999888777",
                SedeAsignada = "Sede Este"
            };

            // Act
            var updateResponse = await _client.PutAsJsonAsync($"/api/veedores/{created.Id}", updateDto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var updated = await updateResponse.Content.ReadFromJsonAsync<VeedorResponseDTO>();
            Assert.NotNull(updated);
            Assert.Equal("Despues de Update", updated.Nombre);
            Assert.Equal("Modificado", updated.Apellido);
            Assert.Equal("Sede Este", updated.SedeAsignada);
        }

        [Fact]
        public async Task DeactivateVeedor_Returns204NoContent()
        {
            // Arrange - Crear veedor inicial
            var uniqueDni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8);
            var uniqueEmail = $"deactivate_{Guid.NewGuid():N}@torneos.com";

            var dto = new VeedorCreateDTO
            {
                Nombre = "Veedor a Desactivar",
                Apellido = "Activo",
                Dni = uniqueDni,
                Email = uniqueEmail
            };

            var createResponse = await _client.PostAsJsonAsync("/api/veedores", dto);
            var created = await createResponse.Content.ReadFromJsonAsync<VeedorResponseDTO>();
            Assert.NotNull(created);

            // Act
            var deleteResponse = await _client.DeleteAsync($"/api/veedores/{created.Id}");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // Verificar que quedó inactivo
            var getResponse = await _client.GetAsync($"/api/veedores/{created.Id}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<VeedorResponseDTO>();
            Assert.NotNull(fetched);
            Assert.False(fetched.Activo);
        }
    }
}
