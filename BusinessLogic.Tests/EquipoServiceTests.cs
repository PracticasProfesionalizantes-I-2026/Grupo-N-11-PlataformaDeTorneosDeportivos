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
    public class EquipoServiceTests
    {
        private readonly Mock<IEquipoRepository> _equipoRepositoryMock;
        private readonly Mock<ITorneoRepository> _torneoRepositoryMock;
        private readonly Mock<IJugadorRepository> _jugadorRepositoryMock;
        private readonly EquipoService _equipoService;

        public EquipoServiceTests()
        {
            _equipoRepositoryMock = new Mock<IEquipoRepository>();
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _jugadorRepositoryMock = new Mock<IJugadorRepository>();

            _equipoService = new EquipoService(_equipoRepositoryMock.Object, _torneoRepositoryMock.Object, _jugadorRepositoryMock.Object);
        }

        [Fact]
        public async Task CambiarEstadoAsync_TorneoLleno_ThrowsBusinessRuleConflictException()
        {
            // Arrange
            var equipoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion };
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 2 };

            var equiposConfirmados = new List<Equipo>
            {
                new Equipo { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado },
                new Equipo { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado }
            };

            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>()))
                                 .ReturnsAsync(equiposConfirmados);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => _equipoService.CambiarEstadoAsync(equipoId, "InscritoYConfirmado"));
            Assert.Contains("cupo máximo", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoAsync_SinJugadores_ThrowsBusinessRuleConflictException()
        {
            // Arrange
            var equipoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion };
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 5 };

            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>()))
                                 .ReturnsAsync(new List<Equipo>()); // Hay cupo
            
            _jugadorRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Jugador, bool>>>()))
                                  .ReturnsAsync(new List<Jugador>()); // 0 jugadores

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => _equipoService.CambiarEstadoAsync(equipoId, "InscritoYConfirmado"));
            Assert.Contains("mínimo de jugadores", ex.Message);
        }
    }
}
