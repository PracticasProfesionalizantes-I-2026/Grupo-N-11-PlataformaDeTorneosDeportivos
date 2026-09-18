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
        private readonly TorneoService _torneoService;

        public TorneoServiceTests()
        {
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _torneoService = new TorneoService(_torneoRepositoryMock.Object);
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

            _torneoRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Torneo>()))
                                 .ReturnsAsync((Torneo t) => { t.Id = Guid.NewGuid(); return t; });

            // Act
            var result = await _torneoService.CreateAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(dto.Nombre, result.Nombre);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Once);
        }
    }
}
