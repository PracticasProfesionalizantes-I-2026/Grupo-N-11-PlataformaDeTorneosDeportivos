using System;
using System.Threading.Tasks;
using BusinessLogic.Security;
using BusinessLogic.Services;
using DataAccess.Entities;
using DataAccess.Repositories;
using Microsoft.Extensions.Configuration;
using Moq;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;
using Xunit;

namespace BusinessLogic.Tests
{
    public class AuthServiceTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _configurationMock = new Mock<IConfiguration>();

            _configurationMock.Setup(c => c[It.Is<string>(s => s == "Jwt:SecretKey")])
                .Returns("ClaveSecretaSuperSeguraParaTorneosDeportivos2026");

            _authService = new AuthService(
                _usuarioRepositoryMock.Object,
                _passwordHasherMock.Object,
                _configurationMock.Object);
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ReturnsToken()
        {
            // Arrange
            var dto = new LoginRequestDTO
            {
                EmailOrDni = "juan@example.com",
                Password = "Password123!"
            };

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = "Juan",
                Apellido = "Pérez",
                Email = "juan@example.com",
                Dni = "12345678",
                PasswordHash = "hash-almacenado",
                Rol = RolUsuario.Organizador,
                Activo = true
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailOrDniAsync(dto.EmailOrDni))
                .ReturnsAsync(usuario);

            _passwordHasherMock.Setup(p => p.VerifyPassword(dto.Password, usuario.PasswordHash))
                .Returns(true);

            // Act
            var result = await _authService.LoginAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.Token));
            Assert.Equal(usuario.Id, result.UsuarioId);
            Assert.Equal("Juan Pérez", result.NombreCompleto);
            Assert.Equal("Organizador", result.Rol);
        }

        [Fact]
        public async Task LoginAsync_WithInvalidCredentials_ThrowsUnauthorizedException_WhenUserNotFound()
        {
            // Arrange
            var dto = new LoginRequestDTO
            {
                EmailOrDni = "inexistente@example.com",
                Password = "Password123!"
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailOrDniAsync(dto.EmailOrDni))
                .ReturnsAsync((Usuario?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(dto));
            Assert.Equal("Usuario o contraseña incorrectos.", ex.Message);
        }

        [Fact]
        public async Task LoginAsync_WithInvalidCredentials_ThrowsUnauthorizedException_WhenPasswordDoesNotMatch()
        {
            // Arrange
            var dto = new LoginRequestDTO
            {
                EmailOrDni = "juan@example.com",
                Password = "PasswordEquivocada"
            };

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = "Juan",
                Apellido = "Pérez",
                Email = "juan@example.com",
                PasswordHash = "hash-almacenado",
                Rol = RolUsuario.Organizador,
                Activo = true
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailOrDniAsync(dto.EmailOrDni))
                .ReturnsAsync(usuario);

            _passwordHasherMock.Setup(p => p.VerifyPassword(dto.Password, usuario.PasswordHash))
                .Returns(false);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(dto));
            Assert.Equal("Usuario o contraseña incorrectos.", ex.Message);
        }

        [Fact]
        public async Task LoginAsync_WhenAccountIsInactive_ThrowsForbiddenException()
        {
            // Arrange
            var dto = new LoginRequestDTO
            {
                EmailOrDni = "juan@example.com",
                Password = "Password123!"
            };

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = "Juan",
                Apellido = "Pérez",
                Email = "juan@example.com",
                PasswordHash = "hash-almacenado",
                Rol = RolUsuario.Organizador,
                Activo = false // Cuenta bloqueada / inactiva
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailOrDniAsync(dto.EmailOrDni))
                .ReturnsAsync(usuario);

            _passwordHasherMock.Setup(p => p.VerifyPassword(dto.Password, usuario.PasswordHash))
                .Returns(true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _authService.LoginAsync(dto));
            Assert.Equal("Cuenta no verificada o inhabilitada. Verifique su correo o contacte al administrador.", ex.Message);
        }
    }
}
