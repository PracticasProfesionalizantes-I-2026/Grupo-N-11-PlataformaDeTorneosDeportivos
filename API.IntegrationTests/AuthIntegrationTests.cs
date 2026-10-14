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
            // Arrange
            var uniqueEmail = $"login.test.{Guid.NewGuid():N}@torneos.com";
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
                EmailOrDni = "",
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
                EmailOrDni = "noexiste@torneos.com",
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
            // Arrange
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
                    Activo = false
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

        [Fact]
        public async Task RecoverPassword_And_ResetPassword_Returns200OK()
        {
            // Arrange - Usuario registrado
            var uniqueEmail = $"recuperar.{Guid.NewGuid():N}@torneos.com";
            var oldPassword = "PasswordVieja123!";
            var newPassword = "PasswordNueva456!";

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

                db.Usuarios.Add(new Usuario
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Usuario",
                    Apellido = "Recuperacion",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    Email = uniqueEmail,
                    PasswordHash = hasher.HashPassword(oldPassword),
                    Rol = RolUsuario.Capitan,
                    Activo = true
                });
                await db.SaveChangesAsync();
            }

            // Act 1 - Solicitar recuperación
            var recoverDto = new RecuperarPasswordRequestDTO { Email = uniqueEmail };
            var recoverResponse = await _client.PostAsJsonAsync("/api/auth/recuperar-password", recoverDto);

            // Assert 1
            Assert.Equal(HttpStatusCode.OK, recoverResponse.StatusCode);
            var recoverResult = await recoverResponse.Content.ReadFromJsonAsync<RecuperarPasswordResponseDTO>();
            Assert.NotNull(recoverResult);
            Assert.False(string.IsNullOrWhiteSpace(recoverResult.Token));

            // Act 2 - Restablecer contraseña con el token
            var resetDto = new ResetPasswordRequestDTO
            {
                Token = recoverResult.Token!,
                NewPassword = newPassword
            };
            var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", resetDto);

            // Assert 2
            Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

            // Act 3 - Login con la nueva contraseña
            var loginDto = new LoginRequestDTO
            {
                EmailOrDni = uniqueEmail,
                Password = newPassword
            };
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginDto);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        }

        [Fact]
        public async Task RecoverPassword_WithNonExistentEmail_Returns200OK()
        {
            // Arrange
            var recoverDto = new RecuperarPasswordRequestDTO
            {
                Email = "emailnoexistente@torneos.com"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/recuperar-password", recoverDto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<RecuperarPasswordResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal("Si el correo existe, se enviará un enlace de recuperación.", result.Mensaje);
        }

        [Fact]
        public async Task ResetPassword_WithInvalidPasswordFormat_Returns400BadRequest()
        {
            // Arrange
            var resetDto = new ResetPasswordRequestDTO
            {
                Token = "cualquier-token",
                NewPassword = "123" // Contraseña no cumple reglas de complejidad
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ResetPassword_WithInvalidToken_Returns400BadRequest()
        {
            // Arrange
            var resetDto = new ResetPasswordRequestDTO
            {
                Token = "token-que-no-existe-en-db",
                NewPassword = "PasswordValida123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
