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
    public class PartidoServiceTests
    {
        private readonly Mock<IPartidoRepository> _partidoRepositoryMock;
        private readonly PartidoService _partidoService;

        public PartidoServiceTests()
        {
            _partidoRepositoryMock = new Mock<IPartidoRepository>();
            _partidoService = new PartidoService(_partidoRepositoryMock.Object);
        }

        [Fact]
        public async Task LoadResultadoAsync_UpdatesScoreAndStatus()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();
            var equipoLocalId = Guid.NewGuid();
            var equipoVisitanteId = Guid.NewGuid();

            var partido = new Partido
            {
                Id = partidoId,
                TorneoId = torneoId,
                Torneo = new Torneo { Id = torneoId, Nombre = "Liga Principal", Modalidad = Modalidad.Liga },
                EquipoLocalId = equipoLocalId,
                EquipoLocal = new Equipo { Id = equipoLocalId, Nombre = "Equipo Alfa" },
                EquipoVisitanteId = equipoVisitanteId,
                EquipoVisitante = new Equipo { Id = equipoVisitanteId, Nombre = "Equipo Beta" },
                Estado = EstadoPartido.Pendiente
            };

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 2,
                MarcadorVisitante = 1
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);

            _partidoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Partido>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _partidoService.LoadResultadoAsync(partidoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(partidoId, result.Id);
            Assert.Equal("Finalizado", result.Estado);
            Assert.Equal(2, result.MarcadorLocal);
            Assert.Equal(1, result.MarcadorVisitante);
            Assert.Equal(equipoLocalId, result.GanadorId);
            Assert.Equal("Equipo Alfa", result.GanadorNombre);
            Assert.Equal("Marcador y estado del partido guardados exitosamente.", result.Mensaje);

            _partidoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Partido>(p =>
                p.Id == partidoId &&
                p.MarcadorLocal == 2 &&
                p.MarcadorVisitante == 1 &&
                p.Estado == EstadoPartido.Finalizado
            )), Times.Once);
        }

        [Fact]
        public async Task LoadResultadoAsync_WhenPartidoAlreadyFinished_ThrowsConflict()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var partido = new Partido
            {
                Id = partidoId,
                Estado = EstadoPartido.Finalizado, // Ya cerrado
                Torneo = new Torneo { Modalidad = Modalidad.Liga }
            };

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 3,
                MarcadorVisitante = 0
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<PartidoEstadoInvalidoException>(() =>
                _partidoService.LoadResultadoAsync(partidoId, dto));

            Assert.Equal("El partido no admite carga de resultados en su estado actual.", ex.Message);
            _partidoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Partido>()), Times.Never);
        }

        [Fact]
        public async Task LoadResultadoAsync_WhenDrawInEliminationPhase_ThrowsConflict()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var partido = new Partido
            {
                Id = partidoId,
                Estado = EstadoPartido.EnJuego,
                Torneo = new Torneo { Modalidad = Modalidad.EliminacionDirecta }
            };

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 1,
                MarcadorVisitante = 1 // Empate sin desempate
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<EmpateNoPermitidoException>(() =>
                _partidoService.LoadResultadoAsync(partidoId, dto));

            Assert.Equal("Esta modalidad no admite empates. Debe definir el ganador (Penales/Tiempo Extra)", ex.Message);
            _partidoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Partido>()), Times.Never);
        }

        [Fact]
        public async Task LoadResultadoAsync_WhenDrawInEliminationPhaseWithValidPenales_UpdatesScoreAndStatus()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var localId = Guid.NewGuid();
            var visitanteId = Guid.NewGuid();

            var partido = new Partido
            {
                Id = partidoId,
                Estado = EstadoPartido.Pendiente,
                Torneo = new Torneo { Modalidad = Modalidad.EliminacionDirecta },
                EquipoLocalId = localId,
                EquipoLocal = new Equipo { Id = localId, Nombre = "Tigres" },
                EquipoVisitanteId = visitanteId,
                EquipoVisitante = new Equipo { Id = visitanteId, Nombre = "Leones" }
            };

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 2,
                MarcadorVisitante = 2,
                PenalesLocal = 5,
                PenalesVisitante = 4
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);

            _partidoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Partido>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _partidoService.LoadResultadoAsync(partidoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Finalizado", result.Estado);
            Assert.Equal(2, result.MarcadorLocal);
            Assert.Equal(2, result.MarcadorVisitante);
            Assert.Equal(5, result.PenalesLocal);
            Assert.Equal(4, result.PenalesVisitante);
            Assert.Equal(localId, result.GanadorId);
            Assert.Equal("Tigres", result.GanadorNombre);

            _partidoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Partido>(p =>
                p.Id == partidoId &&
                p.Estado == EstadoPartido.Finalizado &&
                p.PenalesLocal == 5 &&
                p.PenalesVisitante == 4
            )), Times.Once);
        }

        [Fact]
        public async Task LoadResultadoAsync_WhenPartidoNotFound_ThrowsNotFound()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var dto = new CargarResultadoDTO { MarcadorLocal = 1, MarcadorVisitante = 0 };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync((Partido?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                _partidoService.LoadResultadoAsync(partidoId, dto));

            Assert.Equal($"No se encontró el partido con ID {partidoId}", ex.Message);
            _partidoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Partido>()), Times.Never);
        }

        [Fact]
        public async Task LoadResultadoAsync_WhenNegativeScore_ThrowsValidationException()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = -1,
                MarcadorVisitante = 2
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _partidoService.LoadResultadoAsync(partidoId, dto));

            Assert.Equal("Los marcadores deben ser valores numéricos no negativos.", ex.Message);
            _partidoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Partido>()), Times.Never);
        }
    }
}
