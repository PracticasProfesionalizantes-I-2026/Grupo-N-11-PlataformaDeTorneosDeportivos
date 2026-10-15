using System;
using System.Collections.Generic;
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
    public class InscripcionServiceTests
    {
        private readonly Mock<ITorneoRepository> _torneoRepositoryMock;
        private readonly Mock<IEquipoRepository> _equipoRepositoryMock;
        private readonly InscripcionService _inscripcionService;

        public InscripcionServiceTests()
        {
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _equipoRepositoryMock = new Mock<IEquipoRepository>();

            _inscripcionService = new InscripcionService(
                _torneoRepositoryMock.Object,
                _equipoRepositoryMock.Object);
        }

        [Fact]
        public async Task AprobarInscripcionAsync_WithValidData_ReturnsAprobadoDTOAndDecrementsCupo()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Clausura",
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0m,
                Estado = EstadoTorneo.Abierto
            };

            var equipo = new Equipo
            {
                Id = equipoId,
                Nombre = "Los Halcones",
                TorneoId = torneoId,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                PagoValidado = true
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId))
                .ReturnsAsync(equipo);

            // Hay 3 equipos ya confirmados, quedan cupos
            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>()))
                .ReturnsAsync(new List<Equipo>
                {
                    new() { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado },
                    new() { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado },
                    new() { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado }
                });

            _equipoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Equipo>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _inscripcionService.AprobarInscripcionAsync(torneoId, equipoId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(equipoId, result.EquipoId);
            Assert.Equal(EstadoInscripcion.InscritoYConfirmado.ToString(), result.EstadoInscripcion);
            Assert.Equal(4, result.CuposDisponibles); // 8 - (3 + 1) = 4

            _equipoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Equipo>(e =>
                e.Id == equipoId &&
                e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado &&
                e.EdicionListaHabilitada == false
            )), Times.Once);
        }

        [Fact]
        public async Task AprobarInscripcionAsync_WhenEntityDoesNotExist_ThrowsRecursoNotFoundException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync((Torneo?)null); // Torneo inexistente

            // Act & Assert
            var ex = await Assert.ThrowsAsync<RecursoNotFoundException>(() =>
                _inscripcionService.AprobarInscripcionAsync(torneoId, equipoId));

            Assert.Contains(torneoId.ToString(), ex.Message);
            _equipoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Equipo>()), Times.Never);
        }

        [Fact]
        public async Task AprobarInscripcionAsync_WhenTeamNotPending_ThrowsEstadoEquipoInvalidoException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            var torneo = new Torneo { Id = torneoId, CupoMaximo = 10 };
            var equipoYaConfirmado = new Equipo
            {
                Id = equipoId,
                TorneoId = torneoId,
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado // Ya confirmado
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId))
                .ReturnsAsync(equipoYaConfirmado);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<EstadoEquipoInvalidoException>(() =>
                _inscripcionService.AprobarInscripcionAsync(torneoId, equipoId));

            Assert.Equal("Solo pueden dictaminarse equipos en estado Pendiente de Validación", ex.Message);
            _equipoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Equipo>()), Times.Never);
        }

        [Fact]
        public async Task AprobarInscripcionAsync_WhenTournamentFull_ThrowsTorneoCupoLlenoException()
        {
            // Arrange - RN-06: Torneo lleno (CupoMaximo = 2, ya hay 2 confirmados)
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            var torneo = new Torneo { Id = torneoId, CupoMaximo = 2 };
            var equipo = new Equipo
            {
                Id = equipoId,
                TorneoId = torneoId,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion
            };

            var equiposConfirmados = new List<Equipo>
            {
                new() { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado },
                new() { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado }
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId))
                .ReturnsAsync(equipo);

            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>()))
                .ReturnsAsync(equiposConfirmados);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TorneoCupoLlenoException>(() =>
                _inscripcionService.AprobarInscripcionAsync(torneoId, equipoId));

            Assert.Equal("No se puede confirmar la inscripción: el cupo del torneo se encuentra completo (RN-06)", ex.Message);
            _equipoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Equipo>()), Times.Never);
        }

        [Fact]
        public async Task AprobarInscripcionAsync_WhenPagoNotValidated_ThrowsPagoRequeridoException()
        {
            // Arrange - RN-06: Torneo pago y comprobante de pago no validado
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            var torneo = new Torneo
            {
                Id = torneoId,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 1500m
            };

            var equipoSinPago = new Equipo
            {
                Id = equipoId,
                TorneoId = torneoId,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                PagoValidado = false // Pago no acreditado
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId))
                .ReturnsAsync(equipoSinPago);

            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>()))
                .ReturnsAsync(new List<Equipo>()); // Hay cupo disponible

            // Act & Assert
            var ex = await Assert.ThrowsAsync<PagoRequeridoException>(() =>
                _inscripcionService.AprobarInscripcionAsync(torneoId, equipoId));

            Assert.Equal("No es posible confirmar la inscripción sin acreditar el pago correspondiente (RN-06)", ex.Message);
            _equipoRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Equipo>()), Times.Never);
        }

        [Fact]
        public async Task RechazarInscripcionAsync_WithReason_UnlocksListAndSetsObservado()
        {
            // Arrange - RN-03: Al observar/rechazar, se libera la edición de la lista de buena fe
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8 };
            var equipo = new Equipo
            {
                Id = equipoId,
                Nombre = "Equipo Observado",
                TorneoId = torneoId,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                EdicionListaHabilitada = false
            };

            var dto = new RechazarInscripcionDTO
            {
                MotivoRechazo = "La documentación de los jugadores no es legible y falta foto de DNI."
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId))
                .ReturnsAsync(equipo);

            _equipoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Equipo>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _inscripcionService.RechazarInscripcionAsync(torneoId, equipoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(equipoId, result.EquipoId);
            Assert.Equal(EstadoInscripcion.RechazadoObservado.ToString(), result.EstadoInscripcion);
            Assert.Equal(dto.MotivoRechazo, result.MotivoRechazo);
            Assert.True(result.EdicionListaHabilitada);

            _equipoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Equipo>(e =>
                e.Id == equipoId &&
                e.EstadoInscripcion == EstadoInscripcion.RechazadoObservado &&
                e.MotivoRechazo == dto.MotivoRechazo &&
                e.EdicionListaHabilitada == true // Desbloqueado según RN-03
            )), Times.Once);
        }
    }
}
