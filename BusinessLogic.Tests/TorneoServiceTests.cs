using System;
using System.Threading.Tasks;
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
    public class TorneoServiceTests
    {
        private readonly Mock<ITorneoRepository> _torneoRepositoryMock;
        private readonly Mock<ICuentaRecaudadoraService> _cuentaRecaudadoraMock;
        private readonly TorneoService _torneoService;

        public TorneoServiceTests()
        {
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _cuentaRecaudadoraMock = new Mock<ICuentaRecaudadoraService>();

            _cuentaRecaudadoraMock.Setup(c => c.IsCuentaActivaAsync())
                .ReturnsAsync(true);

            _torneoService = new TorneoService(
                _torneoRepositoryMock.Object,
                _cuentaRecaudadoraMock.Object);
        }

        [Fact]
        public async Task CreateAsync_EliminacionDirectaWithCupoMenorA4_ThrowsBusinessRuleConflictException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Test",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 3,
                CostoInscripcion = 100
            };

            // Act & Assert
            await Assert.ThrowsAsync<BusinessRuleConflictException>(() => _torneoService.CreateAsync(dto));
        }

        [Fact]
        public async Task CreateAsync_CostoInscripcionNegativo_ThrowsValidationException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Test",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                CostoInscripcion = -50
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _torneoService.CreateAsync(dto));
        }

        [Fact]
        public async Task CreateAsync_ValidData_ReturnsTorneoResponseDTO()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Valido",
                Disciplina = Disciplina.ESports,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 8,
                CostoInscripcion = 1000
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(It.IsAny<string>()))
                .ReturnsAsync((Torneo?)null);

            _torneoRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Torneo>()))
                .ReturnsAsync((Torneo t) => { t.Id = Guid.NewGuid(); return t; });

            // Act
            var result = await _torneoService.CreateAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(dto.Nombre, result.Nombre);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Once);
        }

        [Fact]
        public async Task CreateTorneoAsync_WithValidData_ReturnsCreatedTorneoDTO()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Copa de Campeones 2026",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 16,
                EsPago = true,
                CostoInscripcion = 2500m
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync((Torneo?)null);

            _torneoRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Torneo>()))
                .ReturnsAsync((Torneo t) => { t.Id = Guid.NewGuid(); return t; });

            // Act
            var result = await _torneoService.CreateTorneoAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Copa de Campeones 2026", result.Nombre);
            Assert.Equal("Futbol", result.Disciplina);
            Assert.Equal(16, result.CupoMaximo);
            Assert.True(result.EsPago);
            Assert.Equal(2500m, result.CostoInscripcion);
            Assert.Equal(EstadoTorneo.Abierto.ToString(), result.Estado);

            _torneoRepositoryMock.Verify(r => r.AddAsync(It.Is<Torneo>(t =>
                t.Nombre == "Copa de Campeones 2026" &&
                t.CupoMaximo == 16 &&
                t.Estado == EstadoTorneo.Abierto
            )), Times.Once);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenDisciplinaDoesNotExist_ThrowsDisciplinaNotFoundException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Disciplina Invalida",
                Disciplina = (Disciplina)999, // Inexistente
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DisciplinaNotFoundException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("La disciplina especificada no existe", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenNameAlreadyExists_ThrowsTorneoNombreDuplicadoException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Duplicado",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                EsPago = false,
                CostoInscripcion = 0
            };

            var torneoExistente = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = "Torneo Duplicado"
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync(torneoExistente);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TorneoNombreDuplicadoException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("Ya existe un torneo con el nombre ingresado", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenEliminacionDirectaAndCupoNotPowerOfTwo_ThrowsCupoInvalidoEliminacionException()
        {
            // Arrange - RN-04: Cupo 6 no es potencia de 2
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Cupo Invalido",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 6, // Invalido: no es potencia de 2
                EsPago = false,
                CostoInscripcion = 0
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CupoInvalidoEliminacionException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenPagoAndCostoZeroOrNegative_ThrowsCostoInvalidoException()
        {
            // Arrange - RN-05: EsPago == true pero Costo <= 0
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Pago Sin Costo",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 0m // Invalido para torneo pago
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CostoInvalidoException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("El costo debe ser mayor a cero para torneos pagos", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenPagoAndCuentaRecaudadoraInactive_ThrowsCuentaRecaudadoraInactivaException()
        {
            // Arrange - RN-05: Cuenta recaudadora inactiva
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Sin Cuenta",
                Disciplina = Disciplina.ESports,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 1500m,
                CuentaRecaudadoraActiva = false
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CuentaRecaudadoraInactivaException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("No se puede publicar un torneo pago sin una cuenta recaudadora activa (RN-05)", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }
    }
}
