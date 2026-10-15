using System;
using System.Threading.Tasks;
using BusinessLogic.Security;
using BusinessLogic.Services;
using DataAccess.Entities;
using DataAccess.Repositories;
using Moq;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;
using Xunit;

namespace BusinessLogic.Tests
{
    public class UsuarioServiceTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly UsuarioService _usuarioService;

        public UsuarioServiceTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _passwordHasherMock = new Mock<IPasswordHasher>();

            _usuarioService = new UsuarioService(
                _usuarioRepositoryMock.Object,
                _passwordHasherMock.Object);
        }

        [Fact]
        public async Task RegisterUsuarioAsync_WithValidData_ReturnsCreated()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Lucas",
                Apellido = "Fernandez",
                Dni = "40123456",
                Email = "lucas.fernandez@example.com",
                Telefono = "1133445566",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            const string fakePasswordHash = "hash-seguro-123";

            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync(dto.Email.ToLowerInvariant()))
                .ReturnsAsync((Usuario?)null);

            _usuarioRepositoryMock.Setup(r => r.GetByDniAsync(dto.Dni))
                .ReturnsAsync((Usuario?)null);

            _passwordHasherMock.Setup(p => p.HashPassword(dto.Password))
                .Returns(fakePasswordHash);

            _usuarioRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
                .ReturnsAsync((Usuario u) => u);

            // Act
            var result = await _usuarioService.RegisterUsuarioAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("Lucas", result.Nombre);
            Assert.Equal("Fernandez", result.Apellido);
            Assert.Equal("40123456", result.Dni);
            Assert.Equal("lucas.fernandez@example.com", result.Email);
            Assert.Equal("1133445566", result.Telefono);
            Assert.Equal("Capitan", result.Rol);
            Assert.True(result.Activo);
            Assert.Equal("Registro exitoso. Revise su correo para activar la cuenta.", result.Mensaje);

            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.Is<Usuario>(u =>
                u.Email == "lucas.fernandez@example.com" &&
                u.Dni == "40123456" &&
                u.PasswordHash == fakePasswordHash &&
                u.Rol == RolUsuario.Capitan
            )), Times.Once);

            _passwordHasherMock.Verify(p => p.HashPassword("Password123!"), Times.Once);
        }

        [Fact]
        public async Task RegisterUsuarioAsync_WhenEmailOrDniExists_ThrowsConflictException()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Lucas",
                Apellido = "Fernandez",
                Dni = "40123456",
                Email = "duplicado@example.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            var existingUser = new Usuario
            {
                Id = Guid.NewGuid(),
                Email = "duplicado@example.com",
                Dni = "99999999",
                PasswordHash = "hash"
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync(dto.Email.ToLowerInvariant()))
                .ReturnsAsync(existingUser);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UsuarioDuplicadoException>(() =>
                _usuarioService.RegisterUsuarioAsync(dto));

            Assert.Equal("El correo o DNI ya se encuentra registrado en la plataforma.", ex.Message);
            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task RegisterUsuarioAsync_WhenDniAlreadyExists_ThrowsConflictException()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Martin",
                Apellido = "Lopez",
                Dni = "35111222",
                Email = "nuevo@example.com",
                Password = "Password123!",
                Rol = RolUsuario.Espectador
            };

            var existingUserByDni = new Usuario
            {
                Id = Guid.NewGuid(),
                Email = "otro@example.com",
                Dni = "35111222",
                PasswordHash = "hash"
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync(dto.Email.ToLowerInvariant()))
                .ReturnsAsync((Usuario?)null);

            _usuarioRepositoryMock.Setup(r => r.GetByDniAsync(dto.Dni))
                .ReturnsAsync(existingUserByDni);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UsuarioDuplicadoException>(() =>
                _usuarioService.RegisterUsuarioAsync(dto));

            Assert.Equal("El correo o DNI ya se encuentra registrado en la plataforma.", ex.Message);
            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Never);
        }
    }
}
