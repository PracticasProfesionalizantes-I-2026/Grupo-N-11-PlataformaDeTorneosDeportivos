using System;
using System.Collections.Generic;
using System.Linq;
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
    public class FixtureServiceTests
    {
        private readonly Mock<ITorneoRepository> _torneoRepositoryMock;
        private readonly Mock<IPartidoRepository> _partidoRepositoryMock;
        private readonly FixtureService _fixtureService;

        public FixtureServiceTests()
        {
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _partidoRepositoryMock = new Mock<IPartidoRepository>();
            _fixtureService = new FixtureService(_torneoRepositoryMock.Object, _partidoRepositoryMock.Object);
        }

        [Fact]
        public async Task GenerarFixtureAsync_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Apertura 2026",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                Estado = EstadoTorneo.Abierto,
                Equipos = new List<Equipo>
                {
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo A", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo B", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo C", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo D", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId }
                }
            };

            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                Horarios = new List<string> { "14:00", "16:00" },
                Sedes = new List<string> { "Cancha Central", "Cancha Auxiliar" }
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);
            _torneoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Torneo>()))
                .Returns(Task.CompletedTask);
            _partidoRepositoryMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Partido>>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _fixtureService.GenerarFixtureAsync(torneoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(torneoId, result.TorneoId);
            Assert.Equal(EstadoTorneo.EnDesarrollo.ToString(), result.EstadoTorneo);
            Assert.True(result.TotalPartidosGenerados > 0);
            Assert.NotEmpty(result.Partidos);
            Assert.Equal(EstadoTorneo.EnDesarrollo, torneo.Estado);

            _torneoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Torneo>(t => t.Estado == EstadoTorneo.EnDesarrollo)), Times.Once);
            _partidoRepositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Partido>>()), Times.Once);
        }

        [Fact]
        public async Task GenerarFixtureAsync_WithoutMinimumTeams_ThrowsConflictException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Con Pocos Equipos",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                Estado = EstadoTorneo.Abierto,
                Equipos = new List<Equipo>
                {
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo A", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo B", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo C", EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion, TorneoId = torneoId }
                }
            };

            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "Cancha Central" }
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TorneoCupoMinimoNoAlcanzadoException>(() => _fixtureService.GenerarFixtureAsync(torneoId, dto));
            Assert.IsAssignableFrom<BusinessRuleConflictException>(ex);
            Assert.Contains("mínimo de equipos", ex.Message, StringComparison.OrdinalIgnoreCase);

            _torneoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Torneo>()), Times.Never);
            _partidoRepositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Partido>>()), Times.Never);
        }

        [Fact]
        public async Task GenerarFixtureAsync_WhenNotOpen_ThrowsConflictException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Ya En Desarrollo",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                Estado = EstadoTorneo.EnDesarrollo,
                Equipos = new List<Equipo>
                {
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo A", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo B", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo C", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo D", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId }
                }
            };

            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "Cancha Central" }
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TorneoEstadoInvalidoException>(() => _fixtureService.GenerarFixtureAsync(torneoId, dto));
            Assert.IsAssignableFrom<BusinessRuleConflictException>(ex);
            Assert.Contains("Abierto", ex.Message, StringComparison.OrdinalIgnoreCase);

            _torneoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Torneo>()), Times.Never);
            _partidoRepositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Partido>>()), Times.Never);
        }

        [Fact]
        public async Task GenerarFixtureAsync_WhenTorneoNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(7),
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "Cancha Central" }
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync((Torneo?)null);
            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _fixtureService.GenerarFixtureAsync(torneoId, dto));

            _torneoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Torneo>()), Times.Never);
            _partidoRepositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Partido>>()), Times.Never);
        }

        [Fact]
        public async Task GenerarFixtureAsync_EliminacionDirecta_ReturnsSuccess()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Copa Eliminacion 2026",
                Disciplina = Disciplina.ESports,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 4,
                Estado = EstadoTorneo.Abierto,
                Equipos = new List<Equipo>
                {
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Clan Alfa", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Clan Beta", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Clan Gamma", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId },
                    new Equipo { Id = Guid.NewGuid(), Nombre = "Clan Delta", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId }
                }
            };

            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(5),
                Horarios = new List<string> { "18:00", "20:00" },
                Sedes = new List<string> { "Servidor 1" }
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);
            _torneoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Torneo>()))
                .Returns(Task.CompletedTask);
            _partidoRepositoryMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Partido>>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _fixtureService.GenerarFixtureAsync(torneoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(torneoId, result.TorneoId);
            Assert.Equal(2, result.TotalPartidosGenerados);
            Assert.Equal(EstadoTorneo.EnDesarrollo, torneo.Estado);

            _torneoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Torneo>(t => t.Estado == EstadoTorneo.EnDesarrollo)), Times.Once);
            _partidoRepositoryMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Partido>>()), Times.Once);
        }
    }
}
