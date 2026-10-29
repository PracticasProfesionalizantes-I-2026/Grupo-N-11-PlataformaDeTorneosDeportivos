using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    public class UsuariosIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;

        public UsuariosIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task RegisterUsuario_ReturnsSuccessAndCreated()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Agustin",
                Apellido = "Rossi",
                Dni = "55667788",
                Email = "agustin.rossi@torneos.com",
                Telefono = "1122334455",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<UsuarioResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal("Agustin", result.Nombre);
            Assert.Equal("agustin.rossi@torneos.com", result.Email);
            Assert.Equal("Capitan", result.Rol);
            Assert.True(result.Activo);
        }

        [Fact]
        public async Task RegisterUsuario_WithInvalidPassword_Returns400BadRequest()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Test",
                Apellido = "Inseguro",
                Dni = "99887766",
                Email = "inseguro@torneos.com",
                Password = "123" // Contraseña corta e insegura
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RegisterUsuario_WhenDuplicateData_Returns409Conflict()
        {
            // Arrange - Primer registro exitoso
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Original",
                Apellido = "Usuario",
                Dni = "11221122",
                Email = "original@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Organizador
            };

            var firstResponse = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

            // Act - Intento de registro con el mismo DNI y Email
            var duplicateResponse = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        }

        [Fact]
        public async Task RegisterUsuario_WithLettersInDni_Returns400BadRequest()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Test",
                Apellido = "Invalido",
                Dni = "1234567A",
                Email = "test.dniinvalido@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
