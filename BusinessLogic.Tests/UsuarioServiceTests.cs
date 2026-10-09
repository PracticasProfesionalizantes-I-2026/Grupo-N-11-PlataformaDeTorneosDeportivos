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
            _usuarioService = new UsuarioService(_usuarioRepositoryMock.Object, _passwordHasherMock.Object);
        }

        [Fact]
        public async Task RegistrarAsync_ConDatosValidos_RetornaUsuarioCreado()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Franco",
                Apellido = "Sosa",
                Dni = "40123456",
                Email = "franco.sosa@example.com",
                Telefono = "3511234567",
                Password = "PasswordSegura123!"
            };

            _usuarioRepositoryMock.Setup(r => r.ExistePorDniAsync("40123456")).ReturnsAsync(false);
            _usuarioRepositoryMock.Setup(r => r.ExistePorEmailAsync("franco.sosa@example.com")).ReturnsAsync(false);
            _passwordHasherMock.Setup(h => h.HashPassword(dto.Password)).Returns("hashed_secret");
            _usuarioRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
                                  .ReturnsAsync((Usuario u) => { u.Id = Guid.NewGuid(); return u; });

            // Act
            var result = await _usuarioService.RegistrarAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Franco", result.Nombre);
            Assert.Equal("Sosa", result.Apellido);
            Assert.Equal("40123456", result.Dni);
            Assert.Equal("franco.sosa@example.com", result.Email);
            Assert.Equal(RolUsuario.Espectador.ToString(), result.Rol);
            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Once);
        }

        [Fact]
        public async Task RegistrarAsync_DniExistente_LanzaBusinessRuleConflictException()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Franco",
                Apellido = "Sosa",
                Dni = "40123456",
                Email = "otro.email@example.com",
                Password = "PasswordSegura123!"
            };

            _usuarioRepositoryMock.Setup(r => r.ExistePorDniAsync("40123456")).ReturnsAsync(true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => _usuarioService.RegistrarAsync(dto));
            Assert.Contains("40123456", ex.Message);
            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task RegistrarAsync_EmailExistente_LanzaBusinessRuleConflictException()
        {
            // Arrange
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Franco",
                Apellido = "Sosa",
                Dni = "40123456",
                Email = "repetido@example.com",
                Password = "PasswordSegura123!"
            };

            _usuarioRepositoryMock.Setup(r => r.ExistePorDniAsync("40123456")).ReturnsAsync(false);
            _usuarioRepositoryMock.Setup(r => r.ExistePorEmailAsync("repetido@example.com")).ReturnsAsync(true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => _usuarioService.RegistrarAsync(dto));
            Assert.Contains("repetido@example.com", ex.Message);
            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Never);
        }
    }
}
