using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BusinessLogic.Services;
using DataAccess.Entities;
using DataAccess.Repositories;
using Moq;
using Shared.DTOs;
using Shared.Exceptions;
using Xunit;

namespace BusinessLogic.Tests
{
    public class JugadorServiceTests
    {
        private readonly Mock<IJugadorRepository> _jugadorRepositoryMock;
        private readonly Mock<IEquipoRepository> _equipoRepositoryMock;
        private readonly JugadorService _jugadorService;

        public JugadorServiceTests()
        {
            _jugadorRepositoryMock = new Mock<IJugadorRepository>();
            _equipoRepositoryMock = new Mock<IEquipoRepository>();
            _jugadorService = new JugadorService(_jugadorRepositoryMock.Object, _equipoRepositoryMock.Object);
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
    }
}
