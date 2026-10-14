using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shared.DTOs;
using Shared.Enums;
using DataAccess.Data;
using DataAccess.Entities;
using BusinessLogic.Security;
using Xunit;

namespace API.IntegrationTests
{
    public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public AuthIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Login_WithValidCredentials_Returns200OK()
        {
            // Arrange - Insertar usuario activo en la base de datos
            var uniqueEmail = $"organizador.{Guid.NewGuid():N}@torneos.com";
            var password = "Password123!";

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

                db.Usuarios.Add(new Usuario
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Carlos",
                    Apellido = "Organizador",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    Email = uniqueEmail,
                    PasswordHash = hasher.HashPassword(password),
                    Rol = RolUsuario.Organizador,
                    Activo = true
                });
                await db.SaveChangesAsync();
            }

            var loginDto = new LoginRequestDTO
            {
                EmailOrDni = uniqueEmail,
                Password = password
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>();
            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.Token));
            Assert.Equal("Carlos Organizador", result.NombreCompleto);
            Assert.Equal("Organizador", result.Rol);
        }

        [Fact]
        public async Task Login_WithMissingCredentials_Returns400BadRequest()
        {
            // Arrange
            var loginDto = new LoginRequestDTO
            {
                EmailOrDni = "", // Invalido
                Password = ""
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_Returns401Unauthorized()
        {
            // Arrange
            var loginDto = new LoginRequestDTO
            {
                EmailOrDni = "inexistente@torneos.com",
                Password = "Password123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_WhenAccountIsInactive_Returns403Forbidden()
        {
            // Arrange - Insertar usuario inactivo en la base de datos
            var uniqueEmail = $"inactivo.{Guid.NewGuid():N}@torneos.com";
            var password = "Password123!";

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

                db.Usuarios.Add(new Usuario
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Inactivo",
                    Apellido = "Usuario",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    Email = uniqueEmail,
                    PasswordHash = hasher.HashPassword(password),
                    Rol = RolUsuario.Capitan,
                    Activo = false // Cuenta inhabilitada
                });
                await db.SaveChangesAsync();
            }

            var loginDto = new LoginRequestDTO
            {
                EmailOrDni = uniqueEmail,
                Password = password
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
