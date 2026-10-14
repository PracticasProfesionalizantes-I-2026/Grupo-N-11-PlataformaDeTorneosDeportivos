using System;
using System.Threading.Tasks;
using BusinessLogic.Security;
using BusinessLogic.Services;
using DataAccess.Entities;
using DataAccess.Repositories;
using Moq;
using Shared.DTOs;
using Shared.Exceptions;
using Xunit;

namespace BusinessLogic.Tests
{
    public class RecuperacionPasswordServiceTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly RecuperacionPasswordService _service;

        public RecuperacionPasswordServiceTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _service = new RecuperacionPasswordService(_usuarioRepositoryMock.Object, _passwordHasherMock.Object);
        }

        [Fact]
        public async Task RecoverPasswordAsync_WithValidEmail_GeneratesToken()
        {
            // Arrange
            var email = "usuario@example.com";
            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = "old_hash"
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync(usuario);

            // Act
            var result = await _service.SolicitarRecuperacionAsync(new RecuperarPasswordDTO { Email = email });

            // Assert
            Assert.True(result);
            Assert.NotNull(usuario.PasswordResetToken);
            Assert.NotNull(usuario.PasswordResetTokenExpiration);
            Assert.True(usuario.PasswordResetTokenExpiration > DateTime.UtcNow);
            _usuarioRepositoryMock.Verify(r => r.UpdateAsync(usuario), Times.Once);
        }

        [Fact]
        public async Task RecoverPasswordAsync_WithNonExistentEmail_DoesNothing()
        {
            // Arrange
            var email = "inexistente@example.com";
            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync((Usuario?)null);

            // Act
            var result = await _service.SolicitarRecuperacionAsync(new RecuperarPasswordDTO { Email = email });

            // Assert
            Assert.True(result);
            _usuarioRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_UpdatesPassword()
        {
            // Arrange
            var token = "token_valido_123";
            var newPassword = "NuevaPasswordSegura123!";
            var newHash = "nuevo_hash_pbkdf2";

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Email = "usuario@example.com",
                PasswordHash = "hash_viejo",
                PasswordResetToken = token,
                PasswordResetTokenExpiration = DateTime.UtcNow.AddHours(1)
            };

            _usuarioRepositoryMock.Setup(r => r.GetByResetTokenAsync(token))
                .ReturnsAsync(usuario);

            _passwordHasherMock.Setup(h => h.HashPassword(newPassword))
                .Returns(newHash);

            // Act
            var result = await _service.ResetearPasswordAsync(new ResetPasswordDTO
            {
                Token = token,
                NewPassword = newPassword
            });

            // Assert
            Assert.True(result);
            Assert.Equal(newHash, usuario.PasswordHash);
            Assert.Null(usuario.PasswordResetToken);
            Assert.Null(usuario.PasswordResetTokenExpiration);
            _usuarioRepositoryMock.Verify(r => r.UpdateAsync(usuario), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_WithInvalidToken_ThrowsValidationException()
        {
            // Arrange: Token inexistente
            var tokenInvalido = "token_que_no_existe";
            _usuarioRepositoryMock.Setup(r => r.GetByResetTokenAsync(tokenInvalido))
                .ReturnsAsync((Usuario?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _service.ResetearPasswordAsync(new ResetPasswordDTO
                {
                    Token = tokenInvalido,
                    NewPassword = "NuevaPasswordSegura123!"
                }));

            Assert.Contains("El enlace de recuperación ha expirado", ex.Message);
        }

        [Fact]
        public async Task ResetPasswordAsync_WithExpiredToken_ThrowsValidationException()
        {
            // Arrange: Token que ya expiró en el tiempo
            var tokenExpirado = "token_expirado";
            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                PasswordResetToken = tokenExpirado,
                PasswordResetTokenExpiration = DateTime.UtcNow.AddHours(-2) // Expirado hace 2 horas
            };

            _usuarioRepositoryMock.Setup(r => r.GetByResetTokenAsync(tokenExpirado))
                .ReturnsAsync(usuario);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _service.ResetearPasswordAsync(new ResetPasswordDTO
                {
                    Token = tokenExpirado,
                    NewPassword = "NuevaPasswordSegura123!"
                }));

            Assert.Contains("El enlace de recuperación ha expirado", ex.Message);
        }
    }
}
