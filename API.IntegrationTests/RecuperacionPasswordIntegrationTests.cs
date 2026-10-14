using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.DTOs;
using Xunit;

namespace API.IntegrationTests
{
    public class RecuperacionPasswordIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public RecuperacionPasswordIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task RecoverPassword_And_ResetPassword_Returns200OK()
        {
            // Arrange: El usuario "usuario.test@torneos.com" fue sembrado por DbInitializer
            var recoverDto = new RecuperarPasswordDTO
            {
                Email = "usuario.test@torneos.com"
            };

            // Act 1: Solicitar recuperación
            var recoverResponse = await _client.PostAsJsonAsync("/api/auth/recuperar-password", recoverDto);
            Assert.Equal(HttpStatusCode.OK, recoverResponse.StatusCode);

            // Obtener el token generado en base de datos para simular el clic en el enlace del email
            string? resetToken;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == recoverDto.Email);
                Assert.NotNull(usuario);
                Assert.NotNull(usuario.PasswordResetToken);
                resetToken = usuario.PasswordResetToken;
            }

            // Act 2: Enviar la nueva contraseña con el token
            var resetDto = new ResetPasswordDTO
            {
                Token = resetToken!,
                NewPassword = "NuevaPasswordSegura2026!"
            };

            var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", resetDto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        }

        [Fact]
        public async Task RecoverPassword_WithNonExistentEmail_Returns200OK()
        {
            // Arrange: Flujo 3a (Correo no registrado no debe filtrar información)
            var recoverDto = new RecuperarPasswordDTO
            {
                Email = "nadie@example.com"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/recuperar-password", recoverDto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ResetPassword_WithInvalidPasswordFormat_Returns400BadRequest()
        {
            // Arrange: Flujo 6a (Contraseña con menos de 8 caracteres)
            var resetDto = new ResetPasswordDTO
            {
                Token = "token_de_prueba",
                NewPassword = "123" // Menor a 8 caracteres
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ResetPassword_WithInvalidToken_Returns400BadRequest()
        {
            // Arrange: Flujo 7a (Token que no existe en el sistema)
            var resetDto = new ResetPasswordDTO
            {
                Token = "token_inexistente_9999",
                NewPassword = "PasswordSeguraValida123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
