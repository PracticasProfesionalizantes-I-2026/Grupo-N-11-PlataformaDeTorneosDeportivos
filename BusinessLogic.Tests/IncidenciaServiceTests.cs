using System;
using System.Collections.Generic;
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
    public class IncidenciaServiceTests
    {
        private readonly Mock<IIncidenciaRepository> _incidenciaRepositoryMock;
        private readonly Mock<IPartidoRepository> _partidoRepositoryMock;
        private readonly Mock<IJugadorRepository> _jugadorRepositoryMock;
        private readonly Mock<IEquipoRepository> _equipoRepositoryMock;
        private readonly IncidenciaService _incidenciaService;

        public IncidenciaServiceTests()
        {
            _incidenciaRepositoryMock = new Mock<IIncidenciaRepository>();
            _partidoRepositoryMock = new Mock<IPartidoRepository>();
            _jugadorRepositoryMock = new Mock<IJugadorRepository>();
            _equipoRepositoryMock = new Mock<IEquipoRepository>();

            _incidenciaService = new IncidenciaService(
                _incidenciaRepositoryMock.Object,
                _partidoRepositoryMock.Object,
                _jugadorRepositoryMock.Object,
                _equipoRepositoryMock.Object
            );
        }

        [Fact]
        public async Task RegistrarIncidenciaAsync_WithValidData_ReturnsCreatedIncidenciaAndUpdatesScore()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var equipoLocalId = Guid.NewGuid();
            var equipoVisitanteId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();

            var partido = new Partido
            {
                Id = partidoId,
                EquipoLocalId = equipoLocalId,
                EquipoLocal = new Equipo { Id = equipoLocalId, Nombre = "Equipo Local" },
                EquipoVisitanteId = equipoVisitanteId,
                EquipoVisitante = new Equipo { Id = equipoVisitanteId, Nombre = "Equipo Visitante" },
                Estado = EstadoPartido.EnJuego,
                MarcadorLocal = 0,
                MarcadorVisitante = 0
            };

            var jugador = new Jugador
            {
                Id = jugadorId,
                Nombre = "Lionel",
                Apellido = "Messi",
                Dni = "12345678",
                EquipoId = equipoLocalId,
                Inhabilitado = false
            };

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = 25,
                JugadorId = jugadorId,
                EquipoId = equipoLocalId,
                Observaciones = "Golazo de tiro libre"
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);
            _jugadorRepositoryMock.Setup(r => r.GetByIdAsync(jugadorId))
                .ReturnsAsync(jugador);
            _incidenciaRepositoryMock.Setup(r => r.GetByPartidoYJugadorAsync(partidoId, jugadorId))
                .ReturnsAsync(new List<Incidencia>());
            _incidenciaRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Incidencia>()))
                .ReturnsAsync((Incidencia i) => i);
            _partidoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Partido>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _incidenciaService.RegistrarIncidenciaAsync(partidoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(partidoId, result.PartidoId);
            Assert.Equal(equipoLocalId, result.EquipoId);
            Assert.Equal(jugadorId, result.JugadorId);
            Assert.Equal(1, result.MarcadorLocalParcial);
            Assert.Equal(0, result.MarcadorVisitanteParcial);
            Assert.False(result.JugadorExpulsado);
            Assert.Equal("Habilitado", result.EstadoDisciplinarioJugador);
            _partidoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Partido>(p => p.MarcadorLocal == 1)), Times.Once);
            _incidenciaRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Incidencia>()), Times.Once);
        }

        [Fact]
        public async Task RegistrarIncidenciaAsync_WhenPartidoDoesNotExist_ThrowsPartidoNotFoundException()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = 10,
                JugadorId = Guid.NewGuid(),
                EquipoId = Guid.NewGuid()
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync((Partido?)null);

            // Act & Assert
            await Assert.ThrowsAsync<PartidoNotFoundException>(() =>
                _incidenciaService.RegistrarIncidenciaAsync(partidoId, dto));
        }

        [Fact]
        public async Task RegistrarIncidenciaAsync_WhenPartidoNotEditable_ThrowsPartidoEstadoInvalidoException()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var partido = new Partido
            {
                Id = partidoId,
                Estado = EstadoPartido.Finalizado
            };

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = 10,
                JugadorId = Guid.NewGuid(),
                EquipoId = Guid.NewGuid()
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);

            // Act & Assert
            await Assert.ThrowsAsync<PartidoEstadoInvalidoException>(() =>
                _incidenciaService.RegistrarIncidenciaAsync(partidoId, dto));
        }

        [Fact]
        public async Task RegistrarIncidenciaAsync_WhenPlayerNotInTeams_ThrowsJugadorNoAsignadoException()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var equipoLocalId = Guid.NewGuid();
            var equipoVisitanteId = Guid.NewGuid();
            var equipoAjenoId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();

            var partido = new Partido
            {
                Id = partidoId,
                EquipoLocalId = equipoLocalId,
                EquipoVisitanteId = equipoVisitanteId,
                Estado = EstadoPartido.EnJuego
            };

            var jugadorAjeno = new Jugador
            {
                Id = jugadorId,
                EquipoId = equipoAjenoId,
                Inhabilitado = false
            };

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Falta,
                Minuto = 15,
                JugadorId = jugadorId,
                EquipoId = equipoAjenoId
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);
            _jugadorRepositoryMock.Setup(r => r.GetByIdAsync(jugadorId))
                .ReturnsAsync(jugadorAjeno);

            // Act & Assert
            await Assert.ThrowsAsync<JugadorNoAsignadoException>(() =>
                _incidenciaService.RegistrarIncidenciaAsync(partidoId, dto));
        }

        [Fact]
        public async Task RegistrarIncidenciaAsync_WhenPlayerSuspended_ThrowsJugadorInhabilitadoException()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var equipoLocalId = Guid.NewGuid();
            var equipoVisitanteId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();

            var partido = new Partido
            {
                Id = partidoId,
                EquipoLocalId = equipoLocalId,
                EquipoVisitanteId = equipoVisitanteId,
                Estado = EstadoPartido.EnJuego
            };

            var jugadorInhabilitado = new Jugador
            {
                Id = jugadorId,
                EquipoId = equipoLocalId,
                Inhabilitado = true
            };

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.TarjetaAmarilla,
                Minuto = 12,
                JugadorId = jugadorId,
                EquipoId = equipoLocalId
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);
            _jugadorRepositoryMock.Setup(r => r.GetByIdAsync(jugadorId))
                .ReturnsAsync(jugadorInhabilitado);

            // Act & Assert
            await Assert.ThrowsAsync<JugadorInhabilitadoException>(() =>
                _incidenciaService.RegistrarIncidenciaAsync(partidoId, dto));
        }

        [Fact]
        public async Task RegistrarIncidenciaAsync_WhenDirectRedCard_SetsPlayerExpulsado()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var equipoLocalId = Guid.NewGuid();
            var equipoVisitanteId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();

            var partido = new Partido
            {
                Id = partidoId,
                EquipoLocalId = equipoLocalId,
                EquipoVisitanteId = equipoVisitanteId,
                Estado = EstadoPartido.EnJuego
            };

            var jugador = new Jugador
            {
                Id = jugadorId,
                EquipoId = equipoLocalId,
                Nombre = "Carlos",
                Apellido = "Tevez",
                Inhabilitado = false
            };

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.TarjetaRoja,
                Minuto = 44,
                JugadorId = jugadorId,
                EquipoId = equipoLocalId
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);
            _jugadorRepositoryMock.Setup(r => r.GetByIdAsync(jugadorId))
                .ReturnsAsync(jugador);
            _incidenciaRepositoryMock.Setup(r => r.GetByPartidoYJugadorAsync(partidoId, jugadorId))
                .ReturnsAsync(new List<Incidencia>());
            _incidenciaRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Incidencia>()))
                .ReturnsAsync((Incidencia i) => i);

            // Act
            var result = await _incidenciaService.RegistrarIncidenciaAsync(partidoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.JugadorExpulsado);
            Assert.Equal("Expulsado", result.EstadoDisciplinarioJugador);
        }

        [Fact]
        public async Task RegistrarIncidenciaAsync_WhenSecondYellow_SetsPlayerExpulsado()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var equipoLocalId = Guid.NewGuid();
            var equipoVisitanteId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();

            var partido = new Partido
            {
                Id = partidoId,
                EquipoLocalId = equipoLocalId,
                EquipoVisitanteId = equipoVisitanteId,
                Estado = EstadoPartido.EnJuego
            };

            var jugador = new Jugador
            {
                Id = jugadorId,
                EquipoId = equipoLocalId,
                Nombre = "Javier",
                Apellido = "Mascherano",
                Inhabilitado = false
            };

            var incidenciasPrevias = new List<Incidencia>
            {
                new Incidencia
                {
                    Id = Guid.NewGuid(),
                    PartidoId = partidoId,
                    JugadorId = jugadorId,
                    EquipoId = equipoLocalId,
                    TipoIncidencia = TipoIncidencia.TarjetaAmarilla,
                    Minuto = 20
                }
            };

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.TarjetaAmarilla,
                Minuto = 65,
                JugadorId = jugadorId,
                EquipoId = equipoLocalId
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);
            _jugadorRepositoryMock.Setup(r => r.GetByIdAsync(jugadorId))
                .ReturnsAsync(jugador);
            _incidenciaRepositoryMock.Setup(r => r.GetByPartidoYJugadorAsync(partidoId, jugadorId))
                .ReturnsAsync(incidenciasPrevias);
            _incidenciaRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Incidencia>()))
                .ReturnsAsync((Incidencia i) => i);

            // Act
            var result = await _incidenciaService.RegistrarIncidenciaAsync(partidoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.JugadorExpulsado);
            Assert.Equal("Expulsado", result.EstadoDisciplinarioJugador);
            Assert.Contains("RN-07", result.Mensaje);
        }

        [Fact]
        public async Task RegistrarIncidenciaAsync_WhenPlayerAlreadyExpulsado_ThrowsJugadorExpulsadoException()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var equipoLocalId = Guid.NewGuid();
            var equipoVisitanteId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();

            var partido = new Partido
            {
                Id = partidoId,
                EquipoLocalId = equipoLocalId,
                EquipoVisitanteId = equipoVisitanteId,
                Estado = EstadoPartido.EnJuego
            };

            var jugador = new Jugador
            {
                Id = jugadorId,
                EquipoId = equipoLocalId,
                Inhabilitado = false
            };

            var incidenciasPrevias = new List<Incidencia>
            {
                new Incidencia
                {
                    Id = Guid.NewGuid(),
                    PartidoId = partidoId,
                    JugadorId = jugadorId,
                    EquipoId = equipoLocalId,
                    TipoIncidencia = TipoIncidencia.TarjetaRoja,
                    Minuto = 30
                }
            };

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Falta,
                Minuto = 50,
                JugadorId = jugadorId,
                EquipoId = equipoLocalId
            };

            _partidoRepositoryMock.Setup(r => r.GetByIdWithTorneoAndEquiposAsync(partidoId))
                .ReturnsAsync(partido);
            _jugadorRepositoryMock.Setup(r => r.GetByIdAsync(jugadorId))
                .ReturnsAsync(jugador);
            _incidenciaRepositoryMock.Setup(r => r.GetByPartidoYJugadorAsync(partidoId, jugadorId))
                .ReturnsAsync(incidenciasPrevias);

            // Act & Assert
            await Assert.ThrowsAsync<JugadorExpulsadoException>(() =>
                _incidenciaService.RegistrarIncidenciaAsync(partidoId, dto));
        }
    }
}
