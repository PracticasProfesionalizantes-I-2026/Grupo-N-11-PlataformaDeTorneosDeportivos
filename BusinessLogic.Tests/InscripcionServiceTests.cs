using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BusinessLogic.Services;
using DataAccess.Entities;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
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
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IJugadorRepository> _jugadorRepositoryMock;
        private readonly InscripcionService _inscripcionService;

        public InscripcionServiceTests()
        {
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _equipoRepositoryMock = new Mock<IEquipoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _jugadorRepositoryMock = new Mock<IJugadorRepository>();

            _inscripcionService = new InscripcionService(
                _torneoRepositoryMock.Object,
                _equipoRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _jugadorRepositoryMock.Object);
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
        public async Task AprobarInscripcionAsync_WhenLastSpotConfirmed_ClosesTournamentInscriptions()
        {
            // Arrange - Torneo con CupoMaximo = 2, ya hay 1 confirmado
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Dos Cupos",
                CupoMaximo = 2,
                EsPago = false,
                CostoInscripcion = 0m,
                Estado = EstadoTorneo.Abierto
            };

            var equipo = new Equipo
            {
                Id = equipoId,
                Nombre = "Segundo Equipo",
                TorneoId = torneoId,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                PagoValidado = true
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId))
                .ReturnsAsync(equipo);

            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>()))
                .ReturnsAsync(new List<Equipo>
                {
                    new() { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado }
                });

            _equipoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Equipo>()))
                .Returns(Task.CompletedTask);

            _torneoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Torneo>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _inscripcionService.AprobarInscripcionAsync(torneoId, equipoId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.CuposDisponibles);
            Assert.Equal(EstadoTorneo.InscripcionesCerradas, torneo.Estado);

            _torneoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Torneo>(t =>
                t.Id == torneoId && t.Estado == EstadoTorneo.InscripcionesCerradas)), Times.Once);
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

        [Fact]
        public async Task AprobarInscripcionAsync_ConcurrencyConflictOnTorneo_ThrowsDbUpdateConcurrencyException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = false, CostoInscripcion = 0m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion, PagoValidado = true };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>())).ReturnsAsync(new List<Equipo>());
            _torneoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Torneo>())).ThrowsAsync(new DbUpdateConcurrencyException("Conflicto de concurrencia optimista al actualizar cupo"));

            // Act & Assert
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => _inscripcionService.AprobarInscripcionAsync(torneoId, equipoId));

            // Verificaciones ACID
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(default), Times.Never);
        }

        [Fact]
        public async Task RechazarInscripcionAsync_WhenDatabaseFails_RollsBackTransaction()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = false, CostoInscripcion = 0m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion };
            var dto = new RechazarInscripcionDTO { MotivoRechazo = "Documentación vencida" };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _equipoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Equipo>()))
                .ThrowsAsync(new InvalidOperationException("Error de conexión a base de datos al rechazar"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _inscripcionService.RechazarInscripcionAsync(torneoId, equipoId, dto));

            // Verificaciones ACID
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(default), Times.Never);
        }

        [Fact]
        public async Task CambiarEstadoEquipoAsync_TorneoPagoSinPagoValidado_ThrowsPagoRequeridoException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = true, CostoInscripcion = 1500m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion, PagoValidado = false };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<PagoRequeridoException>(() =>
                _inscripcionService.CambiarEstadoEquipoAsync(equipoId, "InscritoYConfirmado"));
            Assert.Contains("acreditar el pago", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoEquipoAsync_TorneoLleno_ThrowsTorneoCupoLlenoException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 2, EsPago = false, CostoInscripcion = 0m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion, PagoValidado = true };

            var equiposConfirmados = new List<Equipo>
            {
                new() { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado },
                new() { Id = Guid.NewGuid(), EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado }
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>())).ReturnsAsync(equiposConfirmados);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TorneoCupoLlenoException>(() =>
                _inscripcionService.CambiarEstadoEquipoAsync(equipoId, "InscritoYConfirmado"));
            Assert.Contains("cupo máximo", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoEquipoAsync_EstadoOrigenInvalido_ThrowsEstadoEquipoInvalidoException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = false, CostoInscripcion = 0m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);

            // Act & Assert (Intentar cambiar estado cuando ya está confirmado)
            var ex = await Assert.ThrowsAsync<EstadoEquipoInvalidoException>(() =>
                _inscripcionService.CambiarEstadoEquipoAsync(equipoId, "InscritoYConfirmado"));
            Assert.Contains("Pendiente de Validación", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoEquipoAsync_ExitosoInscritoYConfirmado_SetsEdicionListaHabilitadaFalse()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = true, CostoInscripcion = 1000m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion, PagoValidado = true, ComprobanteUrl = "https://example.com/pago.pdf", EdicionListaHabilitada = true };
            var jugadores = new List<Jugador>
            {
                new() { Id = Guid.NewGuid(), Nombre = "Leo", Apellido = "Messi", Dni = "10101010", EquipoId = equipoId }
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>())).ReturnsAsync(new List<Equipo>());
            _jugadorRepositoryMock.Setup(j => j.FindAsync(It.IsAny<Expression<Func<Jugador, bool>>>())).ReturnsAsync(jugadores);

            // Act
            var result = await _inscripcionService.CambiarEstadoEquipoAsync(equipoId, "InscritoYConfirmado");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("InscritoYConfirmado", result.EstadoInscripcion);
            Assert.False(equipo.EdicionListaHabilitada);
            _equipoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Equipo>(e => e.Id == equipoId && e.EdicionListaHabilitada == false)), Times.Once);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(default), Times.Once);
        }

        [Fact]
        public async Task CambiarEstadoEquipoAsync_ExitosoRechazadoObservado_SetsEdicionListaHabilitadaTrue()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = false, CostoInscripcion = 0m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion, EdicionListaHabilitada = false };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _jugadorRepositoryMock.Setup(j => j.FindAsync(It.IsAny<Expression<Func<Jugador, bool>>>())).ReturnsAsync(new List<Jugador>());

            // Act
            var result = await _inscripcionService.CambiarEstadoEquipoAsync(equipoId, "RechazadoObservado", "Faltan certificados médicos");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("RechazadoObservado", result.EstadoInscripcion);
            Assert.True(equipo.EdicionListaHabilitada);
            Assert.Equal("Faltan certificados médicos", equipo.MotivoRechazo);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(default), Times.Once);
        }

        [Fact]
        public async Task CambiarEstadoEquipoAsync_ExitosoPendienteDeValidacion_SetsEdicionListaHabilitadaFalse()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = false, CostoInscripcion = 0m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.RechazadoObservado, EdicionListaHabilitada = true };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _jugadorRepositoryMock.Setup(j => j.FindAsync(It.IsAny<Expression<Func<Jugador, bool>>>())).ReturnsAsync(new List<Jugador>());

            // Act
            var result = await _inscripcionService.CambiarEstadoEquipoAsync(equipoId, "PendienteDeValidacion");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PendienteDeValidacion", result.EstadoInscripcion);
            Assert.False(equipo.EdicionListaHabilitada);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(default), Times.Once);
        }

        [Fact]
        public async Task CambiarEstadoEquipoAsync_SinJugadores_ThrowsBusinessRuleConflictException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = false, CostoInscripcion = 0m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion, PagoValidado = true };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>())).ReturnsAsync(new List<Equipo>());
            _jugadorRepositoryMock.Setup(j => j.FindAsync(It.IsAny<Expression<Func<Jugador, bool>>>())).ReturnsAsync(new List<Jugador>()); // 0 jugadores

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
                _inscripcionService.CambiarEstadoEquipoAsync(equipoId, "InscritoYConfirmado"));
            Assert.Contains("mínimo de jugadores", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoEquipoAsync_WhenDatabaseFails_RollsBackTransaction()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = false, CostoInscripcion = 0m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion, PagoValidado = true };
            var jugadores = new List<Jugador> { new() { Id = Guid.NewGuid(), Nombre = "Leo", Apellido = "Messi", Dni = "10101010", EquipoId = equipoId } };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);
            _equipoRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Equipo, bool>>>())).ReturnsAsync(new List<Equipo>());
            _jugadorRepositoryMock.Setup(j => j.FindAsync(It.IsAny<Expression<Func<Jugador, bool>>>())).ReturnsAsync(jugadores);
            _equipoRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Equipo>()))
                .ThrowsAsync(new InvalidOperationException("Fallo en base de datos al cambiar estado"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _inscripcionService.CambiarEstadoEquipoAsync(equipoId, "InscritoYConfirmado"));

            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(default), Times.Never);
        }

        [Fact]
        public async Task ValidarPagoAsync_TorneoOEquipoInexistente_ThrowsRecursoNotFoundException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync((Torneo?)null);

            // Act & Assert
            await Assert.ThrowsAsync<RecursoNotFoundException>(() =>
                _inscripcionService.ValidarPagoAsync(torneoId, equipoId));
        }

        [Fact]
        public async Task ValidarPagoAsync_TorneoPagoSinComprobanteCargadoNiEnDTO_ThrowsPagoRequeridoException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, EsPago = true, CostoInscripcion = 1500m };
            var equipo = new Equipo { Id = equipoId, TorneoId = torneoId, ComprobanteUrl = null, PagoValidado = false };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<PagoRequeridoException>(() =>
                _inscripcionService.ValidarPagoAsync(torneoId, equipoId, new ValidarPagoDTO { ComprobanteUrl = null }));
            Assert.Contains("sin un comprobante de pago", ex.Message);
        }

        [Fact]
        public async Task ValidarPagoAsync_ConComprobanteExistente_HabilitaPagoValidadoTrue()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, Nombre = "Copa Oro", EsPago = true, CostoInscripcion = 2000m };
            var equipo = new Equipo
            {
                Id = equipoId,
                Nombre = "Los Tigres",
                TorneoId = torneoId,
                ComprobanteUrl = "https://comprobantes.com/pago1.png",
                PagoValidado = false
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);

            // Act
            var result = await _inscripcionService.ValidarPagoAsync(torneoId, equipoId);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.PagoValidado);
            Assert.True(equipo.PagoValidado);
            Assert.Equal("https://comprobantes.com/pago1.png", result.ComprobanteUrl);
            _equipoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Equipo>(e => e.Id == equipoId && e.PagoValidado == true)), Times.Once);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(default), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(default), Times.Once);
        }

        [Fact]
        public async Task ValidarPagoAsync_SuministrandoComprobanteEnDTO_ActualizaComprobanteYHabilitaPagoValidadoTrue()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, Nombre = "Copa Oro", EsPago = true, CostoInscripcion = 2000m };
            var equipo = new Equipo
            {
                Id = equipoId,
                Nombre = "Los Tigres",
                TorneoId = torneoId,
                ComprobanteUrl = null,
                PagoValidado = false
            };
            var dto = new ValidarPagoDTO { ComprobanteUrl = "https://comprobantes.com/nuevo_pago.pdf" };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);

            // Act
            var result = await _inscripcionService.ValidarPagoAsync(torneoId, equipoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.PagoValidado);
            Assert.Equal("https://comprobantes.com/nuevo_pago.pdf", equipo.ComprobanteUrl);
            Assert.True(equipo.PagoValidado);
            _equipoRepositoryMock.Verify(r => r.UpdateAsync(It.Is<Equipo>(e =>
                e.Id == equipoId &&
                e.PagoValidado == true &&
                e.ComprobanteUrl == "https://comprobantes.com/nuevo_pago.pdf")), Times.Once);
        }

        [Fact]
        public async Task AprobarInscripcionAsync_TorneoPagoConPagoValidadoPeroSinComprobanteUrl_ThrowsPagoRequeridoException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var torneo = new Torneo { Id = torneoId, CupoMaximo = 8, EsPago = true, CostoInscripcion = 1500m, Estado = EstadoTorneo.Abierto };
            var equipo = new Equipo
            {
                Id = equipoId,
                TorneoId = torneoId,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                PagoValidado = true,
                ComprobanteUrl = null // Sin comprobante
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId)).ReturnsAsync(torneo);
            _equipoRepositoryMock.Setup(r => r.GetByIdAsync(equipoId)).ReturnsAsync(equipo);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<PagoRequeridoException>(() =>
                _inscripcionService.AprobarInscripcionAsync(torneoId, equipoId));
            Assert.Contains("acreditar el pago", ex.Message);
        }
    }
}
