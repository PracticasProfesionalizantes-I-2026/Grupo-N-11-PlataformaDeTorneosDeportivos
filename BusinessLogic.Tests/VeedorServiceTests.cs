using System;
using System.Collections.Generic;
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
    public class VeedorServiceTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<IPartidoRepository> _partidoRepositoryMock;
        private readonly VeedorService _veedorService;

        public VeedorServiceTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _partidoRepositoryMock = new Mock<IPartidoRepository>();

            _passwordHasherMock.Setup(p => p.HashPassword(It.IsAny<string>()))
                .Returns("hashed_secret_password");

            _veedorService = new VeedorService(
                _usuarioRepositoryMock.Object,
                _passwordHasherMock.Object,
                _partidoRepositoryMock.Object);
        }

        [Fact]
        public async Task CreateVeedorAsync_WithValidData_ReturnsCreated()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var dto = new VeedorCreateDTO
            {
                Nombre = "Roberto",
                Apellido = "Gomez",
                Dni = "33444555",
                Email = "veedor.roberto@torneos.com",
                Telefono = "1122334455",
                SedeAsignada = "Cancha Principal",
                TorneosAsignados = new List<Guid> { torneoId }
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync("veedor.roberto@torneos.com"))
                .ReturnsAsync((Usuario?)null);

            _usuarioRepositoryMock.Setup(r => r.GetByDniAsync("33444555"))
                .ReturnsAsync((Usuario?)null);

            _usuarioRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
                .ReturnsAsync((Usuario u) => u);

            // Act
            var result = await _veedorService.CreateVeedorAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Roberto", result.Nombre);
            Assert.Equal("Gomez", result.Apellido);
            Assert.Equal("33444555", result.Dni);
            Assert.Equal("veedor.roberto@torneos.com", result.Email);
            Assert.Equal(RolUsuario.Veedor.ToString(), result.Rol);
            Assert.True(result.Activo);
            Assert.Contains(torneoId, result.TorneosAsignados);

            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.Is<Usuario>(u =>
                u.Nombre == "Roberto" &&
                u.Dni == "33444555" &&
                u.Rol == RolUsuario.Veedor &&
                u.Activo == true
            )), Times.Once);
        }

        [Fact]
        public async Task CreateVeedorAsync_WithExistingDni_ThrowsConflictException()
        {
            // Arrange - DNI ya registrado
            var dto = new VeedorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Duplicado",
                Dni = "12345678",
                Email = "nuevo.email@torneos.com"
            };

            var existingUser = new Usuario
            {
                Id = Guid.NewGuid(),
                Dni = "12345678",
                Email = "otro.email@torneos.com"
            };

            _usuarioRepositoryMock.Setup(r => r.GetByEmailAsync("nuevo.email@torneos.com"))
                .ReturnsAsync((Usuario?)null);

            _usuarioRepositoryMock.Setup(r => r.GetByDniAsync("12345678"))
                .ReturnsAsync(existingUser);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
                _veedorService.CreateVeedorAsync(dto));

            Assert.Equal("El DNI o Email ya están registrados en el sistema", ex.Message);
            _usuarioRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task UpdateVeedorAsync_WithValidData_UpdatesVeedor()
        {
            // Arrange
            var veedorId = Guid.NewGuid();
            var veedorExistente = new Usuario
            {
                Id = veedorId,
                Nombre = "Carlos",
                Apellido = "Viejo",
                Dni = "44555666",
                Email = "carlos@torneos.com",
                Rol = RolUsuario.Veedor,
                Activo = true
            };

            var updateDto = new VeedorUpdateDTO
            {
                Nombre = "Carlos Alberto",
                Apellido = "Nuevo",
                Telefono = "9988776655",
                SedeAsignada = "Sede Norte"
            };

            _usuarioRepositoryMock.Setup(r => r.GetByIdAsync(veedorId))
                .ReturnsAsync(veedorExistente);

            _usuarioRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Usuario>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _veedorService.UpdateVeedorAsync(veedorId, updateDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Carlos Alberto", result.Nombre);
            Assert.Equal("Nuevo", result.Apellido);
            Assert.Equal("9988776655", result.Telefono);
            Assert.Equal("Sede Norte", result.SedeAsignada);

            _usuarioRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Usuario>(u =>
                u.Id == veedorId &&
                u.Nombre == "Carlos Alberto" &&
                u.Apellido == "Nuevo"
            )), Times.Once);
        }

        [Fact]
        public async Task DeactivateVeedorAsync_UpdatesStatus()
        {
            // Arrange
            var veedorId = Guid.NewGuid();
            var veedorActivo = new Usuario
            {
                Id = veedorId,
                Nombre = "Lucas",
                Apellido = "Veedor",
                Dni = "77889900",
                Rol = RolUsuario.Veedor,
                Activo = true
            };

            _usuarioRepositoryMock.Setup(r => r.GetByIdAsync(veedorId))
                .ReturnsAsync(veedorActivo);

            _usuarioRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Usuario>()))
                .Returns(Task.CompletedTask);

            // Act
            await _veedorService.DeactivateVeedorAsync(veedorId);

            // Assert
            Assert.False(veedorActivo.Activo);
            _usuarioRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Usuario>(u =>
                u.Id == veedorId &&
                u.Activo == false
            )), Times.Once);
        }

        [Fact]
        public async Task GetPartidosByVeedorAsync_ReturnsListaPartidos()
        {
            // Arrange
            var veedorId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();
            var veedor = new Usuario
            {
                Id = veedorId,
                Nombre = "Carlos",
                Apellido = "Veedor",
                Rol = RolUsuario.Veedor,
                Activo = true,
                TorneosAsignadosIds = torneoId.ToString()
            };

            var partidos = new List<Partido>
            {
                new Partido
                {
                    Id = Guid.NewGuid(),
                    TorneoId = torneoId,
                    Torneo = new Torneo { Id = torneoId, Nombre = "Torneo Apertura" },
                    EquipoLocalId = Guid.NewGuid(),
                    EquipoLocal = new Equipo { Nombre = "Equipo A" },
                    EquipoVisitanteId = Guid.NewGuid(),
                    EquipoVisitante = new Equipo { Nombre = "Equipo B" },
                    FechaHora = DateTime.Today.AddHours(14),
                    Sede = "Cancha 1",
                    Ronda = 1,
                    Estado = EstadoPartido.Pendiente,
                    VeedorId = veedorId
                }
            };

            _usuarioRepositoryMock.Setup(r => r.GetByIdAsync(veedorId))
                .ReturnsAsync(veedor);

            _partidoRepositoryMock.Setup(r => r.GetPartidosByVeedorAsync(veedorId, It.IsAny<IEnumerable<Guid>>(), null, null))
                .ReturnsAsync(partidos);

            // Act
            var result = await _veedorService.GetPartidosByVeedorAsync(veedorId, null, null, veedorId, "Veedor");

            // Assert
            Assert.NotNull(result);
            var list = Assert.IsAssignableFrom<IEnumerable<PartidoCronogramaResponseDTO>>(result);
            Assert.Single(list);
            var partidoDto = list.First();
            Assert.Equal("Torneo Apertura", partidoDto.TorneoNombre);
            Assert.Equal("Equipo A", partidoDto.EquipoLocalNombre);
            Assert.Equal("Equipo B", partidoDto.EquipoVisitanteNombre);
            Assert.Equal("Cancha 1", partidoDto.Sede);
            Assert.Equal("Pendiente", partidoDto.Estado);
            Assert.Equal(veedorId, partidoDto.VeedorId);

            _usuarioRepositoryMock.Verify(r => r.GetByIdAsync(veedorId), Times.Once);
            _partidoRepositoryMock.Verify(r => r.GetPartidosByVeedorAsync(veedorId, It.IsAny<IEnumerable<Guid>>(), null, null), Times.Once);
        }

        [Fact]
        public async Task GetPartidosByVeedorAsync_WhenEmpty_ReturnsEmptyList()
        {
            // Arrange
            var veedorId = Guid.NewGuid();
            var veedor = new Usuario
            {
                Id = veedorId,
                Nombre = "Carlos",
                Apellido = "Veedor",
                Rol = RolUsuario.Veedor,
                Activo = true
            };

            _usuarioRepositoryMock.Setup(r => r.GetByIdAsync(veedorId))
                .ReturnsAsync(veedor);

            _partidoRepositoryMock.Setup(r => r.GetPartidosByVeedorAsync(veedorId, It.IsAny<IEnumerable<Guid>>(), null, null))
                .ReturnsAsync(new List<Partido>());

            // Act
            var result = await _veedorService.GetPartidosByVeedorAsync(veedorId, null, null, veedorId, "Veedor");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _partidoRepositoryMock.Verify(r => r.GetPartidosByVeedorAsync(veedorId, It.IsAny<IEnumerable<Guid>>(), null, null), Times.Once);
        }

        [Fact]
        public async Task GetPartidosByVeedorAsync_WithDifferentUserId_ThrowsForbidden()
        {
            // Arrange
            var veedorId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                _veedorService.GetPartidosByVeedorAsync(veedorId, null, null, otherUserId, "Veedor"));

            Assert.Equal("No tiene permisos para consultar el cronograma de otro veedor.", ex.Message);
            _usuarioRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
            _partidoRepositoryMock.Verify(r => r.GetPartidosByVeedorAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), null, null), Times.Never);
        }

        [Fact]
        public async Task GetPartidosByVeedorAsync_WhenVeedorNotFound_ThrowsNotFound()
        {
            // Arrange
            var veedorId = Guid.NewGuid();

            _usuarioRepositoryMock.Setup(r => r.GetByIdAsync(veedorId))
                .ReturnsAsync((Usuario?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                _veedorService.GetPartidosByVeedorAsync(veedorId, null, null, veedorId, "Veedor"));

            Assert.Equal($"No se encontró el veedor con ID {veedorId}", ex.Message);
        }

        [Fact]
        public async Task CreateVeedorAsync_WhenDniContainsLetters_ThrowsValidationException()
        {
            // Arrange
            var dto = new VeedorCreateDTO
            {
                Nombre = "Roberto",
                Apellido = "Gomez",
                Dni = "3344455A",
                Email = "veedor.invalido@torneos.com"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _veedorService.CreateVeedorAsync(dto));

            Assert.Equal("El DNI debe contener solo números.", ex.Message);
        }
    }
}
