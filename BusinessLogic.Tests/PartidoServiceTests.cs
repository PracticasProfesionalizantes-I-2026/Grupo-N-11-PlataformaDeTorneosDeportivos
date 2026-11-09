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
        private readonly Mock<ITorneoRepository> _torneoRepositoryMock;
        private readonly PartidoService _partidoService;

        public PartidoServiceTests()
        {
            _partidoRepositoryMock = new Mock<IPartidoRepository>();
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _partidoService = new PartidoService(_partidoRepositoryMock.Object, _torneoRepositoryMock.Object);
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

        [Fact]
        public async Task LoadResultadoAsync_EliminacionDirecta_WhenBothSemifinalsFinished_CreatesFinalMatch()
        {
            // Arrange - Torneo 4 equipos (Semifinal 1 y Semifinal 2 en Ronda 1)
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Eliminación 4",
                Modalidad = Modalidad.EliminacionDirecta,
                Estado = EstadoTorneo.EnDesarrollo
            };

            var eqA = Guid.NewGuid();
            var eqB = Guid.NewGuid();
            var eqC = Guid.NewGuid();
            var eqD = Guid.NewGuid();

            var semi1 = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneoId,
                Torneo = torneo,
                EquipoLocalId = eqA,
                EquipoVisitanteId = eqB,
                MarcadorLocal = 2,
                MarcadorVisitante = 1,
                Ronda = 1,
                FechaHora = DateTime.Today.AddHours(10),
                Estado = EstadoPartido.Finalizado // Ya finalizó Semifinal 1 (ganó eqA)
            };

            var semi2 = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneoId,
                Torneo = torneo,
                EquipoLocalId = eqC,
                EquipoVisitanteId = eqD,
                Ronda = 1,
                FechaHora = DateTime.Today.AddHours(12),
                Estado = EstadoPartido.Pendiente // Ahora va a finalizar Semifinal 2 (gana eqC)
            };

            var listaPartidos = new System.Collections.Generic.List<Partido> { semi1, semi2 };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(semi2.Id))
                .ReturnsAsync(semi2);

            _partidoRepositoryMock.Setup(r => r.GetPartidosByTorneoIdAsync(torneoId))
                .ReturnsAsync(listaPartidos);

            _partidoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Partido>()))
                .Returns(Task.CompletedTask);

            _partidoRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Partido>()))
                .ReturnsAsync((Partido p) => p);

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 3,
                MarcadorVisitante = 0 // Gana eqC
            };

            // Act - Cargar resultado de Semifinal 2
            var result = await _partidoService.LoadResultadoAsync(semi2.Id, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(eqC, result.GanadorId);

            // Debe haberse creado el partido de la Ronda 2 (Final) enfrentando al ganador de semi1 vs ganador de semi2
            _partidoRepositoryMock.Verify(r => r.AddAsync(It.Is<Partido>(p =>
                p.Ronda == 2 &&
                p.TorneoId == torneoId &&
                p.EquipoLocalId == eqA &&
                p.EquipoVisitanteId == eqC &&
                p.Estado == EstadoPartido.Pendiente
            )), Times.Once);

            // El torneo NO debe haberse finalizado aún (sigue EnDesarrollo)
            Assert.Equal(EstadoTorneo.EnDesarrollo, torneo.Estado);
            _torneoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task LoadResultadoAsync_EliminacionDirecta_WhenOnlyOneMatchOfPairFinished_DoesNotCreateNextRound()
        {
            // Arrange - Torneo 4 equipos (Solo finaliza Semifinal 1, Semifinal 2 sigue pendiente)
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Eliminación 4",
                Modalidad = Modalidad.EliminacionDirecta,
                Estado = EstadoTorneo.EnDesarrollo
            };

            var eqA = Guid.NewGuid();
            var eqB = Guid.NewGuid();
            var eqC = Guid.NewGuid();
            var eqD = Guid.NewGuid();

            var semi1 = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneoId,
                Torneo = torneo,
                EquipoLocalId = eqA,
                EquipoVisitanteId = eqB,
                Ronda = 1,
                FechaHora = DateTime.Today.AddHours(10),
                Estado = EstadoPartido.Pendiente
            };

            var semi2 = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneoId,
                Torneo = torneo,
                EquipoLocalId = eqC,
                EquipoVisitanteId = eqD,
                Ronda = 1,
                FechaHora = DateTime.Today.AddHours(12),
                Estado = EstadoPartido.Pendiente
            };

            var listaPartidos = new System.Collections.Generic.List<Partido> { semi1, semi2 };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(semi1.Id))
                .ReturnsAsync(semi1);

            _partidoRepositoryMock.Setup(r => r.GetPartidosByTorneoIdAsync(torneoId))
                .ReturnsAsync(listaPartidos);

            _partidoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Partido>()))
                .Returns(Task.CompletedTask);

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 1,
                MarcadorVisitante = 0
            };

            // Act
            var result = await _partidoService.LoadResultadoAsync(semi1.Id, dto);

            // Assert
            Assert.NotNull(result);
            _partidoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Partido>()), Times.Never);
            _torneoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task LoadResultadoAsync_EliminacionDirecta_WhenFinalMatchFinished_DeclaresChampionAndFinalizesTorneo()
        {
            // Arrange - Partido Final (Ronda 2, único partido en la ronda)
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Eliminación 4",
                Modalidad = Modalidad.EliminacionDirecta,
                Estado = EstadoTorneo.EnDesarrollo
            };

            var eqA = Guid.NewGuid();
            var eqC = Guid.NewGuid();

            var finalMatch = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneoId,
                Torneo = torneo,
                EquipoLocalId = eqA,
                EquipoVisitanteId = eqC,
                Ronda = 2,
                FechaHora = DateTime.Today.AddDays(1).AddHours(10),
                Estado = EstadoPartido.Pendiente
            };

            var listaPartidos = new System.Collections.Generic.List<Partido> { finalMatch };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(finalMatch.Id))
                .ReturnsAsync(finalMatch);

            _partidoRepositoryMock.Setup(r => r.GetPartidosByTorneoIdAsync(torneoId))
                .ReturnsAsync(listaPartidos);

            _partidoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Partido>()))
                .Returns(Task.CompletedTask);

            _torneoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Torneo>()))
                .Returns(Task.CompletedTask);

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 2,
                MarcadorVisitante = 1 // Gana eqA
            };

            // Act - Cargar resultado de la Final
            var result = await _partidoService.LoadResultadoAsync(finalMatch.Id, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Finalizado", result.Estado);
            Assert.Equal(eqA, result.GanadorId);

            // Se debe haber actualizado el torneo pasando a Finalizado y declarando a eqA como Campeón
            Assert.Equal(EstadoTorneo.Finalizado, torneo.Estado);
            Assert.Equal(eqA, torneo.CampeonId);

            _torneoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Torneo>(t =>
                t.Id == torneoId &&
                t.Estado == EstadoTorneo.Finalizado &&
                t.CampeonId == eqA
            )), Times.Once);
        }
    }
}
