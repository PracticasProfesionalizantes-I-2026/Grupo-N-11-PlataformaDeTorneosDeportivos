using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
    public class JugadorServiceTests
    {
        private readonly Mock<IJugadorRepository> _jugadorRepositoryMock;
        private readonly Mock<IEquipoRepository> _equipoRepositoryMock;
        private readonly Mock<IIncidenciaRepository> _incidenciaRepositoryMock;
        private readonly Mock<ITorneoRepository> _torneoRepositoryMock;
        private readonly JugadorService _jugadorService;

        public JugadorServiceTests()
        {
            _jugadorRepositoryMock = new Mock<IJugadorRepository>();
            _equipoRepositoryMock = new Mock<IEquipoRepository>();
            _incidenciaRepositoryMock = new Mock<IIncidenciaRepository>();
            _torneoRepositoryMock = new Mock<ITorneoRepository>();

            _jugadorService = new JugadorService(
                _jugadorRepositoryMock.Object, 
                _equipoRepositoryMock.Object,
                _incidenciaRepositoryMock.Object,
                _torneoRepositoryMock.Object);
        }

        [Fact]
        public async Task CreateAsync_JugadorDuplicadoEnMismoTorneo_ThrowsBusinessRuleConflictException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoDestinoId = Guid.NewGuid();
            var equipoExistenteId = Guid.NewGuid();

            var equipoDestino = new Equipo { Id = equipoDestinoId, TorneoId = torneoId };
            var equipoExistente = new Equipo { Id = equipoExistenteId, TorneoId = torneoId, Nombre = "Equipo A" };
            
            var dto = new JugadorCreateDTO { Dni = "123456", EquipoId = equipoDestinoId };

            var jugadorExistente = new Jugador { Id = Guid.NewGuid(), Dni = "123456", EquipoId = equipoExistenteId };

            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoDestinoId)).ReturnsAsync(equipoDestino);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoExistenteId)).ReturnsAsync(equipoExistente);
            
            _jugadorRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Jugador, bool>>>()))
                                  .ReturnsAsync(new List<Jugador> { jugadorExistente });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => _jugadorService.CreateAsync(dto));
            Assert.Contains("ya se encuentra inscrito", ex.Message);
        }

        [Fact]
        public async Task GetEstadisticasJugadorAsync_ReturnsConsolidatedData()
        {
            // Arrange (CU-14 Flujo Principal)
            var jugadorId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();

            var torneo = new Torneo { Id = torneoId, Nombre = "Torneo Apertura 2026" };
            var equipo = new Equipo { Id = equipoId, Nombre = "Los Rayos FC", TorneoId = torneoId, Torneo = torneo };
            var jugador = new Jugador
            {
                Id = jugadorId,
                Nombre = "Lionel",
                Apellido = "Messi",
                Dni = "10101010",
                EquipoId = equipoId,
                Equipo = equipo
            };

            var partido = new Partido { Id = Guid.NewGuid(), TorneoId = torneoId, Torneo = torneo };

            var incidencias = new List<Incidencia>
            {
                new() { Id = Guid.NewGuid(), JugadorId = jugadorId, TipoIncidencia = TipoIncidencia.Gol, Partido = partido },
                new() { Id = Guid.NewGuid(), JugadorId = jugadorId, TipoIncidencia = TipoIncidencia.Gol, Partido = partido },
                new() { Id = Guid.NewGuid(), JugadorId = jugadorId, TipoIncidencia = TipoIncidencia.Gol, Partido = partido },
                new() { Id = Guid.NewGuid(), JugadorId = jugadorId, TipoIncidencia = TipoIncidencia.TarjetaAmarilla, Partido = partido },
                new() { Id = Guid.NewGuid(), JugadorId = jugadorId, TipoIncidencia = TipoIncidencia.TarjetaRoja, Partido = partido },
                new() { Id = Guid.NewGuid(), JugadorId = jugadorId, TipoIncidencia = TipoIncidencia.Falta, Partido = partido }
            };

            _jugadorRepositoryMock.Setup(r => r.GetByIdWithEquipoAndTorneoAsync(jugadorId))
                .ReturnsAsync(jugador);

            _incidenciaRepositoryMock.Setup(r => r.GetByJugadorIdAsync(jugadorId))
                .ReturnsAsync(incidencias);

            _incidenciaRepositoryMock.Setup(r => r.GetByJugadorDniAsync(jugador.Dni))
                .ReturnsAsync(new List<Incidencia>());

            _jugadorRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Jugador, bool>>>()))
                .ReturnsAsync(new List<Jugador> { jugador });

            // Act
            var result = await _jugadorService.GetEstadisticasAsync(jugadorId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(jugadorId, result.JugadorId);
            Assert.Equal("Lionel", result.Nombre);
            Assert.Equal("Messi", result.Apellido);
            Assert.Equal("Lionel Messi", result.NombreCompleto);
            Assert.Equal("10101010", result.Dni);
            Assert.Equal(equipoId, result.EquipoActualId);
            Assert.Equal("Los Rayos FC", result.EquipoActual);
            Assert.Equal(3, result.Goles);
            Assert.Equal(1, result.TarjetasAmarillas);
            Assert.Equal(1, result.TarjetasRojas);
            Assert.Equal(1, result.Faltas);
            Assert.Equal(1, result.TorneosDisputados);
            Assert.Contains("Torneo Apertura 2026", result.HistorialTorneos);
        }

        [Fact]
        public async Task GetEstadisticasJugadorAsync_WhenNotFound_ThrowsNotFoundException()
        {
            // Arrange (CU-14 Flujo Alternativo 3a)
            var jugadorId = Guid.NewGuid();
            _jugadorRepositoryMock.Setup(r => r.GetByIdWithEquipoAndTorneoAsync(jugadorId))
                .ReturnsAsync((Jugador?)null);
            _jugadorRepositoryMock.Setup(r => r.GetByIdAsync(jugadorId))
                .ReturnsAsync((Jugador?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => 
                _jugadorService.GetEstadisticasAsync(jugadorId));
        }

        [Fact]
        public async Task GetEstadisticasJugadorAsync_WithHistoryAcrossMultipleTournaments_ReturnsAllTorneos()
        {
            // Arrange
            var jugadorId = Guid.NewGuid();
            var equipo1Id = Guid.NewGuid();
            var equipo2Id = Guid.NewGuid();
            var torneo1Id = Guid.NewGuid();
            var torneo2Id = Guid.NewGuid();

            var torneo1 = new Torneo { Id = torneo1Id, Nombre = "Copa Verano" };
            var torneo2 = new Torneo { Id = torneo2Id, Nombre = "Liga Invierno" };

            var equipo1 = new Equipo { Id = equipo1Id, Nombre = "Equipo 1", TorneoId = torneo1Id, Torneo = torneo1 };
            var equipo2 = new Equipo { Id = equipo2Id, Nombre = "Equipo 2", TorneoId = torneo2Id, Torneo = torneo2 };

            var jugadorActual = new Jugador
            {
                Id = jugadorId,
                Nombre = "Cristiano",
                Apellido = "Ronaldo",
                Dni = "77777777",
                EquipoId = equipo1Id,
                Equipo = equipo1
            };

            var registroHistorico = new Jugador
            {
                Id = Guid.NewGuid(),
                Nombre = "Cristiano",
                Apellido = "Ronaldo",
                Dni = "77777777",
                EquipoId = equipo2Id,
                Equipo = equipo2
            };

            _jugadorRepositoryMock.Setup(r => r.GetByIdWithEquipoAndTorneoAsync(jugadorId))
                .ReturnsAsync(jugadorActual);

            _incidenciaRepositoryMock.Setup(r => r.GetByJugadorIdAsync(jugadorId))
                .ReturnsAsync(new List<Incidencia>());

            _incidenciaRepositoryMock.Setup(r => r.GetByJugadorDniAsync(jugadorActual.Dni))
                .ReturnsAsync(new List<Incidencia>());

            _jugadorRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Jugador, bool>>>()))
                .ReturnsAsync(new List<Jugador> { jugadorActual, registroHistorico });

            // Act
            var result = await _jugadorService.GetEstadisticasAsync(jugadorId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TorneosDisputados);
            Assert.Contains("Copa Verano", result.HistorialTorneos);
            Assert.Contains("Liga Invierno", result.HistorialTorneos);
        }

        [Theory]
        [InlineData("1234567A")]
        [InlineData("ABCDEFGH")]
        [InlineData("33444-555")]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAsync_WhenDniContainsLettersOrInvalidCharacters_ThrowsValidationException(string invalidDni)
        {
            // Arrange
            var dto = new JugadorCreateDTO
            {
                Nombre = "Lionel",
                Apellido = "Messi",
                Dni = invalidDni,
                EquipoId = Guid.NewGuid()
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _jugadorService.CreateAsync(dto));

            Assert.Equal("El DNI debe contener solo números.", ex.Message);
        }
    }
}
