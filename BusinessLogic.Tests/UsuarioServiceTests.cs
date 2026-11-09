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

        [Theory]
        [InlineData("1234567A")]
        [InlineData("ABCDEFGH")]
        [InlineData("40123-456")]
        [InlineData("40 123 456")]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RegisterUsuarioAsync_WhenDniContainsLettersOrInvalidCharacters_ThrowsValidationException(string invalidDni)
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Lucas",
                Apellido = "Fernandez",
                Dni = invalidDni,
                Email = "lucas.fernandez@example.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _usuarioService.RegisterUsuarioAsync(dto));

            Assert.Equal("El DNI debe contener solo números.", ex.Message);
            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Never);
        }

        #region Prevención de Escalada de Privilegios (CU-01 / RNF-04)

        [Theory]
        [InlineData(RolUsuario.Administrador)]
        [InlineData(RolUsuario.Organizador)]
        [InlineData(RolUsuario.Veedor)]
        public async Task RegisterUsuarioAsync_IntentoEscaladaRolElevado_FuerzaRolCapitan(RolUsuario rolElevado)
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Hacker",
                Apellido = "Malicioso",
                Dni = "40999888",
                Email = "hacker@torneos.com",
                Password = "Password123!",
                Rol = rolElevado // Intento de inyectar rol administrativo
            };

            const string fakeHash = "fake-hash-seguro";
            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync(dto.Email.ToLowerInvariant()))
                .ReturnsAsync((Usuario?)null);
            _usuarioRepositoryMock.Setup(r => r.GetByDniAsync(dto.Dni))
                .ReturnsAsync((Usuario?)null);
            _passwordHasherMock.Setup(p => p.HashPassword(dto.Password))
                .Returns(fakeHash);
            _usuarioRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
                .ReturnsAsync((Usuario u) => u);

            // Act
            var result = await _usuarioService.RegisterUsuarioAsync(dto);

            // Assert: El usuario devuelto y persistido NUNCA debe tener el rol elevado solicitado, sino Capitan
            Assert.NotNull(result);
            Assert.Equal("Capitan", result.Rol);

            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.Is<Usuario>(u =>
                u.Rol == RolUsuario.Capitan &&
                u.Rol != rolElevado
            )), Times.Once);
        }

        [Fact]
        public async Task CreateUsuarioAdminAsync_UsuarioSinRolAdministrativo_ThrowsForbiddenException()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Intruso",
                Apellido = "Admin",
                Dni = "38111222",
                Email = "intruso@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Organizador
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                _usuarioService.CreateUsuarioAdminAsync(dto, "Capitan"));

            Assert.Contains("No tiene permisos para dar de alta usuarios con roles administrativos", ex.Message);
            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task CreateUsuarioAdminAsync_Administrador_PermiteAsignarRolElevado()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Nuevo",
                Apellido = "Organizador",
                Dni = "38111333",
                Email = "nuevo.org@torneos.com",
                Password = "Password123!",
                Rol = RolUsuario.Organizador
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync(dto.Email.ToLowerInvariant()))
                .ReturnsAsync((Usuario?)null);
            _usuarioRepositoryMock.Setup(r => r.GetByDniAsync(dto.Dni))
                .ReturnsAsync((Usuario?)null);
            _passwordHasherMock.Setup(p => p.HashPassword(dto.Password))
                .Returns("hash-admin");
            _usuarioRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
                .ReturnsAsync((Usuario u) => u);

            // Act: Invocado legítimamente por un Administrador
            var result = await _usuarioService.CreateUsuarioAdminAsync(dto, "Administrador");

            // Assert: En endpoint protegido administrativo sí se respeta el rol
            Assert.NotNull(result);
            Assert.Equal("Organizador", result.Rol);
            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.Is<Usuario>(u => u.Rol == RolUsuario.Organizador)), Times.Once);
        }

        [Fact]
        public async Task CambiarRolAsync_UsuarioSinRolAdministrativo_ThrowsForbiddenException()
        {
            // Arrange
            var usuarioId = Guid.NewGuid();
            var dto = new CambiarRolDTO { NuevoRol = RolUsuario.Administrador };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                _usuarioService.CambiarRolAsync(usuarioId, dto, "Capitan"));

            Assert.Contains("No tiene permisos para asignar o modificar roles", ex.Message);
            _usuarioRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task CambiarRolAsync_Administrador_ModificaRolCorrectamente()
        {
            // Arrange
            var usuarioId = Guid.NewGuid();
            var usuarioExistente = new Usuario
            {
                Id = usuarioId,
                Nombre = "Franco",
                Apellido = "Serafin",
                Dni = "39111222",
                Email = "franco@torneos.com",
                Rol = RolUsuario.Capitan
            };

            var dto = new CambiarRolDTO { NuevoRol = RolUsuario.Organizador };

            _usuarioRepositoryMock.Setup(r => r.GetByIdAsync(usuarioId))
                .ReturnsAsync(usuarioExistente);
            _usuarioRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Usuario>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _usuarioService.CambiarRolAsync(usuarioId, dto, "Administrador");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Organizador", result.Rol);
            Assert.Equal(RolUsuario.Organizador, usuarioExistente.Rol);
            _usuarioRepositoryMock.Verify(r => r.UpdateAsync(usuarioExistente), Times.Once);
        }

        #endregion
    }
}
