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

        [Fact]
        public async Task FlujoPrimerIngresoVeedor_CambioPasswordObligatorio_FuncionaCorrectamente()
        {
            // 1. Crear veedor por API (recibiendo contraseña provisoria)
            var uniqueDni = $"{Random.Shared.Next(10000000, 99999999)}";
            var uniqueEmail = $"veedor_auth_{Guid.NewGuid():N}@torneos.com";

            var createDto = new VeedorCreateDTO
            {
                Nombre = "Veedor Flujo",
                Apellido = "Autenticacion",
                Dni = uniqueDni,
                Email = uniqueEmail
            };

            var createResponse = await _client.PostAsJsonAsync("/api/veedores", createDto);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var veedorCreado = await createResponse.Content.ReadFromJsonAsync<VeedorResponseDTO>();
            Assert.NotNull(veedorCreado);
            Assert.False(string.IsNullOrWhiteSpace(veedorCreado.PasswordTemporal));
            Assert.True(veedorCreado.RequiereCambioPassword);

            var tempPassword = veedorCreado.PasswordTemporal;

            // 2. Primer login con contraseña provisoria
            var loginDto = new LoginRequestDTO
            {
                EmailOrDni = uniqueEmail,
                Password = tempPassword
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginDto);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDTO>();
            Assert.NotNull(loginResult);
            Assert.True(loginResult.RequiereCambioPassword, "El veedor recién creado debe requerir cambio de contraseña");

            // 3. Intento de cambio con contraseña idéntica a la provisoria -> Debe rechazar (400 Bad Request)
            var cambioMismaClaveDto = new CambiarPasswordInicialDTO
            {
                EmailOrDni = uniqueEmail,
                PasswordActual = tempPassword,
                NuevaPassword = tempPassword
            };

            var rechazoResponse = await _client.PostAsJsonAsync("/api/auth/cambiar-password", cambioMismaClaveDto);
            Assert.Equal(HttpStatusCode.BadRequest, rechazoResponse.StatusCode);

            // 4. Intento de cambio con contraseña actual errónea -> 401 Unauthorized
            var cambioClaveErroneaDto = new CambiarPasswordInicialDTO
            {
                EmailOrDni = uniqueEmail,
                PasswordActual = "ClaveIncorrecta123!",
                NuevaPassword = "NuevaPasswordDefinitiva2026!"
            };

            var errPasswordResponse = await _client.PostAsJsonAsync("/api/auth/cambiar-password", cambioClaveErroneaDto);
            Assert.Equal(HttpStatusCode.Unauthorized, errPasswordResponse.StatusCode);

            // 5. Cambio exitoso de contraseña mediante /api/auth/primer-ingreso
            var nuevaPasswordValida = "NuevaPasswordDefinitiva2026!";
            var cambioValidoDto = new CambiarPasswordInicialDTO
            {
                EmailOrDni = uniqueEmail,
                PasswordActual = tempPassword,
                NuevaPassword = nuevaPasswordValida
            };

            var cambioExitosoResponse = await _client.PostAsJsonAsync("/api/auth/primer-ingreso", cambioValidoDto);
            Assert.Equal(HttpStatusCode.OK, cambioExitosoResponse.StatusCode);
            var cambioResult = await cambioExitosoResponse.Content.ReadFromJsonAsync<CambiarPasswordResponseDTO>();
            Assert.NotNull(cambioResult);
            Assert.True(cambioResult.Exito);
            Assert.False(string.IsNullOrWhiteSpace(cambioResult.Token));

            // 6. Intento de login con la contraseña provisoria anterior -> Debe fallar (401 Unauthorized)
            var loginViejoDto = new LoginRequestDTO
            {
                EmailOrDni = uniqueEmail,
                Password = tempPassword
            };

            var loginViejoResponse = await _client.PostAsJsonAsync("/api/auth/login", loginViejoDto);
            Assert.Equal(HttpStatusCode.Unauthorized, loginViejoResponse.StatusCode);

            // 7. Login con la nueva contraseña definitiva -> 200 OK y RequiereCambioPassword == false
            var loginNuevoDto = new LoginRequestDTO
            {
                EmailOrDni = uniqueEmail,
                Password = nuevaPasswordValida
            };

            var loginNuevoResponse = await _client.PostAsJsonAsync("/api/auth/login", loginNuevoDto);
            Assert.Equal(HttpStatusCode.OK, loginNuevoResponse.StatusCode);
            var loginNuevoResult = await loginNuevoResponse.Content.ReadFromJsonAsync<LoginResponseDTO>();
            Assert.NotNull(loginNuevoResult);
            Assert.False(loginNuevoResult.RequiereCambioPassword, "Tras el cambio obligatorio, la bandera debe quedar en false");
        }
    }
}
