using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Common;
using BusinessLogic.Services;
using DataAccess.Entities;
using DataAccess.Repositories;
using Moq;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace BusinessLogic.Tests
{
    public class StandingsOptimizationTests
    {
        private readonly Mock<ITorneoRepository> _torneoRepositoryMock = new();
        private readonly Mock<IPartidoRepository> _partidoRepositoryMock = new();
        private readonly Mock<ITablaPosicionRepository> _tablaPosicionRepositoryMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

        [Fact]
        public void StandingsCalculator_SortsByPoints_GoalDiff_GoalsFor_AndName()
        {
            var eqA = new Equipo { Id = Guid.NewGuid(), Nombre = "Alfa", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
            var eqB = new Equipo { Id = Guid.NewGuid(), Nombre = "Beta", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
            var eqC = new Equipo { Id = Guid.NewGuid(), Nombre = "Gamma", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };

            var partidos = new List<Partido>
            {
                // Alfa 3 - 0 Beta -> Alfa 3pts, +3 dif, 3 GF. Beta 0pts, -3 dif, 0 GF.
                new Partido
                {
                    Id = Guid.NewGuid(),
                    EquipoLocalId = eqA.Id,
                    EquipoLocal = eqA,
                    EquipoVisitanteId = eqB.Id,
                    EquipoVisitante = eqB,
                    MarcadorLocal = 3,
                    MarcadorVisitante = 0,
                    Estado = EstadoPartido.Finalizado
                },
                // Gamma 1 - 0 Beta -> Gamma 3pts, +1 dif, 1 GF. Beta 0pts, -4 dif, 0 GF.
                new Partido
                {
                    Id = Guid.NewGuid(),
                    EquipoLocalId = eqC.Id,
                    EquipoLocal = eqC,
                    EquipoVisitanteId = eqB.Id,
                    EquipoVisitante = eqB,
                    MarcadorLocal = 1,
                    MarcadorVisitante = 0,
                    Estado = EstadoPartido.Finalizado
                }
            };

            var standings = StandingsCalculator.CalculateStandingsDTOs(new[] { eqA, eqB, eqC }, partidos);

            Assert.Equal(3, standings.Count);
            // 1st: Alfa (3 pts, +3 dif)
            Assert.Equal(1, standings[0].Posicion);
            Assert.Equal(eqA.Id, standings[0].EquipoId);
            Assert.Equal(3, standings[0].Puntos);
            Assert.Equal(3, standings[0].DiferenciaGoles);

            // 2nd: Gamma (3 pts, +1 dif)
            Assert.Equal(2, standings[1].Posicion);
            Assert.Equal(eqC.Id, standings[1].EquipoId);
            Assert.Equal(3, standings[1].Puntos);
            Assert.Equal(1, standings[1].DiferenciaGoles);

            // 3rd: Beta (0 pts, -4 dif)
            Assert.Equal(3, standings[2].Posicion);
            Assert.Equal(eqB.Id, standings[2].EquipoId);
            Assert.Equal(0, standings[2].Puntos);
        }

        [Fact]
        public async Task GetPosicionesAsync_WhenPrecalculatedStandingsExist_ReturnsDirectlyFromRepository()
        {
            var torneoId = Guid.NewGuid();
            var precalculated = new List<TablaPosicion>
            {
                new TablaPosicion
                {
                    Id = Guid.NewGuid(),
                    TorneoId = torneoId,
                    EquipoId = Guid.NewGuid(),
                    EquipoNombre = "Campeón FC",
                    Posicion = 1,
                    Puntos = 15,
                    PartidosJugados = 5,
                    PartidosGanados = 5,
                    GolesAFavor = 12,
                    GolesEnContra = 2,
                    DiferenciaGoles = 10
                }
            };

            _tablaPosicionRepositoryMock.Setup(r => r.GetByTorneoIdAsync(torneoId))
                .ReturnsAsync(precalculated);

            var torneoService = new TorneoService(
                _torneoRepositoryMock.Object,
                cuentaRecaudadoraService: null,
                partidoRepository: _partidoRepositoryMock.Object,
                tablaPosicionRepository: _tablaPosicionRepositoryMock.Object);

            var result = (await torneoService.GetPosicionesAsync(torneoId)).ToList();

            Assert.Single(result);
            Assert.Equal(1, result[0].Posicion);
            Assert.Equal("Campeón FC", result[0].EquipoNombre);
            Assert.Equal(15, result[0].Puntos);

            // Verificamos que no se consultaron los partidos porque se resolvió directamente desde la tabla precalculada
            _partidoRepositoryMock.Verify(r => r.GetPartidosByTorneoIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task GetPosicionesAsync_WhenStandingsNotPrecalculated_ComputesAndPersists()
        {
            var torneoId = Guid.NewGuid();
            var eq1 = new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo Uno", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Apertura",
                Modalidad = Modalidad.Liga,
                Equipos = new List<Equipo> { eq1 }
            };

            // Repositorio retorna vacío inicialmente
            _tablaPosicionRepositoryMock.Setup(r => r.GetByTorneoIdAsync(torneoId))
                .ReturnsAsync(new List<TablaPosicion>());

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);

            _partidoRepositoryMock.Setup(r => r.GetPartidosByTorneoIdAsync(torneoId))
                .ReturnsAsync(new List<Partido>());

            var torneoService = new TorneoService(
                _torneoRepositoryMock.Object,
                cuentaRecaudadoraService: null,
                partidoRepository: _partidoRepositoryMock.Object,
                tablaPosicionRepository: _tablaPosicionRepositoryMock.Object);

            var result = (await torneoService.GetPosicionesAsync(torneoId)).ToList();

            Assert.Single(result);
            Assert.Equal("Equipo Uno", result[0].EquipoNombre);

            // Verificamos que se invocó la persistencia para acelerar futuras consultas
            _tablaPosicionRepositoryMock.Verify(r => r.UpsertPosicionesTorneoAsync(torneoId, It.IsAny<IEnumerable<TablaPosicion>>()), Times.Once);
        }

        [Fact]
        public async Task PartidoService_LoadResultadoAsync_InLiga_UpdatesStandingsTable()
        {
            var torneoId = Guid.NewGuid();
            var eqLocal = new Equipo { Id = Guid.NewGuid(), Nombre = "Local", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
            var eqVis = new Equipo { Id = Guid.NewGuid(), Nombre = "Visita", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };

            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Liga Anual",
                Modalidad = Modalidad.Liga,
                Equipos = new List<Equipo> { eqLocal, eqVis }
            };

            var partidoId = Guid.NewGuid();
            var partido = new Partido
            {
                Id = partidoId,
                TorneoId = torneoId,
                Torneo = torneo,
                EquipoLocalId = eqLocal.Id,
                EquipoLocal = eqLocal,
                EquipoVisitanteId = eqVis.Id,
                EquipoVisitante = eqVis,
                Estado = EstadoPartido.Pendiente
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId)).ReturnsAsync(partido);
            _partidoRepositoryMock.Setup(r => r.GetByIdAsync(partidoId)).ReturnsAsync(partido);
            _partidoRepositoryMock.Setup(r => r.GetPartidosByTorneoIdAsync(torneoId)).ReturnsAsync(new List<Partido> { partido });
            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId)).ReturnsAsync(torneo);

            var partidoService = new PartidoService(
                _partidoRepositoryMock.Object,
                _torneoRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _tablaPosicionRepositoryMock.Object);

            var dto = new CargarResultadoDTO { MarcadorLocal = 2, MarcadorVisitante = 1 };
            var response = await partidoService.LoadResultadoAsync(partidoId, dto);

            Assert.Equal("Finalizado", response.Estado);
            Assert.Equal(2, response.MarcadorLocal);
            Assert.Equal(1, response.MarcadorVisitante);

            // Debe haberse llamado a persistir la tabla de posiciones actualizada
            _tablaPosicionRepositoryMock.Verify(r => r.UpsertPosicionesTorneoAsync(torneoId, It.IsAny<IEnumerable<TablaPosicion>>()), Times.Once);
        }
    }
}
