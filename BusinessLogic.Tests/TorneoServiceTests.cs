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
        private readonly Mock<IDisciplinaRepository> _disciplinaRepositoryMock;
        private readonly Mock<ICuentaRecaudadoraRepository> _cuentaRecaudadoraRepositoryMock;
        private readonly TorneoService _torneoService;

        public TorneoServiceTests()
        {
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _disciplinaRepositoryMock = new Mock<IDisciplinaRepository>();
            _cuentaRecaudadoraRepositoryMock = new Mock<ICuentaRecaudadoraRepository>();

            // Configuraciones por defecto exitosas
            _disciplinaRepositoryMock
                .Setup(d => d.ExistsAndIsActiveAsync(It.IsAny<int>()))
                .ReturnsAsync(true);

            _cuentaRecaudadoraRepositoryMock
                .Setup(c => c.TieneCuentaActivaAsync())
                .ReturnsAsync(true);

            _torneoRepositoryMock
                .Setup(r => r.ExistsByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            _torneoRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<Torneo>()))
                .ReturnsAsync((Torneo t) => { t.Id = Guid.NewGuid(); return t; });

            _torneoService = new TorneoService(
                _torneoRepositoryMock.Object,
                _disciplinaRepositoryMock.Object,
                _cuentaRecaudadoraRepositoryMock.Object);
        }

        [Fact]
        public async Task CreateTorneoAsync_WithValidData_ReturnsCreatedTorneoDTO()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Copa de Campeones",
                DisciplinaId = 0,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 1500m
            };

            // Act
            var result = await _torneoService.CreateTorneoAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("Copa de Campeones", result.Nombre);
            Assert.Equal("Futbol", result.Disciplina);
            Assert.Equal(8, result.CupoMaximo);
            Assert.Equal(1500m, result.CostoInscripcion);
            Assert.True(result.EsPago);
            Assert.Equal("Abierto para Inscripción", result.Estado);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Once);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenDisciplinaDoesNotExist_ThrowsDisciplinaNotFoundException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Fantasma",
                DisciplinaId = 999,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                EsPago = false,
                CostoInscripcion = 0
            };

            _disciplinaRepositoryMock
                .Setup(d => d.ExistsAndIsActiveAsync(999))
                .ReturnsAsync(false);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DisciplinaNotFoundException>(() => _torneoService.CreateTorneoAsync(dto));
            Assert.Equal("La disciplina especificada no existe", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenNameAlreadyExists_ThrowsTorneoNombreDuplicadoException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Repetido",
                DisciplinaId = 0,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                EsPago = false,
                CostoInscripcion = 0
            };

            _torneoRepositoryMock
                .Setup(r => r.ExistsByNameAsync("Torneo Repetido"))
                .ReturnsAsync(true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TorneoNombreDuplicadoException>(() => _torneoService.CreateTorneoAsync(dto));
            Assert.Equal("Ya existe un torneo con el nombre ingresado", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenEliminacionDirectaAndCupoNotPowerOfTwo_ThrowsCupoInvalidoEliminacionException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Brackets 6 Equipos",
                DisciplinaId = 0,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 6, // 6 no es potencia de 2
                EsPago = false,
                CostoInscripcion = 0
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CupoInvalidoEliminacionException>(() => _torneoService.CreateTorneoAsync(dto));
            Assert.Equal("Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenEliminacionDirectaAndCupoMenorA4_ThrowsCupoInvalidoEliminacionException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Brackets 2 Equipos",
                DisciplinaId = 0,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 2, // Menor a 4
                EsPago = false,
                CostoInscripcion = 0
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CupoInvalidoEliminacionException>(() => _torneoService.CreateTorneoAsync(dto));
            Assert.Equal("Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenPagoAndCostoZeroOrNegative_ThrowsCostoInvalidoException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Pago Gratis",
                DisciplinaId = 0,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                EsPago = true,
                CostoInscripcion = 0 // Inválido para torneo pago
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CostoInvalidoException>(() => _torneoService.CreateTorneoAsync(dto));
            Assert.Equal("El costo debe ser mayor a cero para torneos pagos", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenPagoAndCuentaRecaudadoraInactive_ThrowsCuentaRecaudadoraInactivaException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Pago Sin Cuenta",
                DisciplinaId = 0,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                EsPago = true,
                CostoInscripcion = 2000m
            };

            _cuentaRecaudadoraRepositoryMock
                .Setup(c => c.TieneCuentaActivaAsync())
                .ReturnsAsync(false);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CuentaRecaudadoraInactivaException>(() => _torneoService.CreateTorneoAsync(dto));
            Assert.Equal("No se puede publicar un torneo pago sin una cuenta recaudadora activa (RN-05)", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenNombreEsVacio_ThrowsValidationException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "   ",
                DisciplinaId = 0,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(() => _torneoService.CreateTorneoAsync(dto));
            Assert.Equal("El nombre del torneo es obligatorio.", ex.Message);
        }
    }
}
