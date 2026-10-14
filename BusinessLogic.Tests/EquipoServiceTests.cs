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

            _equipoService = new EquipoService(
                _equipoRepositoryMock.Object,
                _torneoRepositoryMock.Object,
                _jugadorRepositoryMock.Object);
        }

        [Fact]
        public async Task CreateEquipoAsync_WithValidData_ReturnsCreatedEquipoDTO()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Apertura",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                Estado = "Abierto para Inscripción"
            };

            var dto = new EquipoCreateDTO
            {
                Nombre = "Los Halcones",
                LogoUrl = "https://example.com/logo.png",
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "11111111" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = "22222222" },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = "33333333" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = "44444444" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = "55555555" }
                }
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.ExistsNombreInTorneoAsync(torneoId, dto.Nombre))
                .ReturnsAsync(false);

            _jugadorRepositoryMock.Setup(r => r.GetJugadoresByTorneoIdAsync(torneoId))
                .ReturnsAsync(new List<Jugador>());

            _equipoRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Equipo>()))
                .ReturnsAsync((Equipo e) => e);

            // Act
            var result = await _equipoService.CreateEquipoAsync(torneoId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Los Halcones", result.Nombre);
            Assert.Equal(EstadoInscripcion.PendienteDeValidacion.ToString(), result.EstadoInscripcion);
            Assert.Equal(5, result.CantidadJugadores);
            Assert.Equal(torneoId, result.TorneoId);

            _equipoRepositoryMock.Verify(r => r.AddAsync(It.Is<Equipo>(e =>
                e.Nombre == "Los Halcones" &&
                e.TorneoId == torneoId &&
                e.EstadoInscripcion == EstadoInscripcion.PendienteDeValidacion &&
                e.Jugadores.Count == 5
            )), Times.Once);
        }

        [Fact]
        public async Task CreateEquipoAsync_WhenTorneoDoesNotExist_ThrowsTorneoNotFoundException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var dto = new EquipoCreateDTO
            {
                Nombre = "Equipo Fantasma",
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "A", Apellido = "B", Dni = "12345678" }
                }
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TorneoNotFoundException>(() =>
                _equipoService.CreateEquipoAsync(torneoId, dto));

            Assert.Equal("El torneo especificado no existe", ex.Message);
            _equipoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Equipo>()), Times.Never);
        }

        [Fact]
        public async Task CreateEquipoAsync_WhenTorneoNotOpen_ThrowsTorneoInscripcionCerradaException()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var torneoCerrado = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo En Curso",
                Disciplina = Disciplina.Futbol,
                Estado = "Inscripciones Cerradas"
            };

            var dto = new EquipoCreateDTO
            {
                Nombre = "Equipo Tardio",
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "A", Apellido = "B", Dni = "12345678" }
                }
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneoCerrado);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TorneoInscripcionCerradaException>(() =>
                _equipoService.CreateEquipoAsync(torneoId, dto));

            Assert.Equal("El torneo no se encuentra disponible para nuevas inscripciones", ex.Message);
            _equipoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Equipo>()), Times.Never);
        }

        [Fact]
        public async Task CreateEquipoAsync_WhenPlayersBelowMinimum_ThrowsMinimoJugadoresInsuficienteException()
        {
            // Arrange - Futbol requiere al menos 5 jugadores; enviamos 3
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Futbol 5",
                Disciplina = Disciplina.Futbol,
                Estado = "Abierto para Inscripción"
            };

            var dto = new EquipoCreateDTO
            {
                Nombre = "Equipo Incompleto",
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "11111111" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = "22222222" },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = "33333333" }
                }
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.ExistsNombreInTorneoAsync(torneoId, dto.Nombre))
                .ReturnsAsync(false);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<MinimoJugadoresInsuficienteException>(() =>
                _equipoService.CreateEquipoAsync(torneoId, dto));

            Assert.Equal("La lista debe contener al menos el número mínimo de jugadores requeridos para la disciplina", ex.Message);
            _equipoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Equipo>()), Times.Never);
        }

        [Fact]
        public async Task CreateEquipoAsync_WhenDuplicateDniInList_ThrowsValidationException()
        {
            // Arrange - Dos jugadores con el mismo DNI "11111111"
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Futbol",
                Disciplina = Disciplina.Futbol,
                Estado = "Abierto para Inscripción"
            };

            var dto = new EquipoCreateDTO
            {
                Nombre = "Equipo Dnis Repetidos",
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "11111111" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = "11111111" }, // Duplicado
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = "33333333" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = "44444444" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = "55555555" }
                }
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.ExistsNombreInTorneoAsync(torneoId, dto.Nombre))
                .ReturnsAsync(false);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _equipoService.CreateEquipoAsync(torneoId, dto));

            Assert.Equal("Se encontraron DNIs duplicados en la lista de jugadores enviada", ex.Message);
            _equipoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Equipo>()), Times.Never);
        }

        [Fact]
        public async Task CreateEquipoAsync_WhenPlayerAlreadyInOtherTeam_ThrowsJugadorYaInscriptoException()
        {
            // Arrange - RN-02: Jugador "22222222" ya está en otro equipo del mismo torneo
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Futbol",
                Disciplina = Disciplina.Futbol,
                Estado = "Abierto para Inscripción"
            };

            var dto = new EquipoCreateDTO
            {
                Nombre = "Equipo Nuevo",
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "11111111" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = "22222222" },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = "33333333" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = "44444444" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = "55555555" }
                }
            };

            var jugadorExistente = new Jugador
            {
                Id = Guid.NewGuid(),
                Nombre = "Carlos",
                Apellido = "Lopez",
                Dni = "22222222",
                EquipoId = Guid.NewGuid()
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.ExistsNombreInTorneoAsync(torneoId, dto.Nombre))
                .ReturnsAsync(false);

            _jugadorRepositoryMock.Setup(r => r.GetJugadoresByTorneoIdAsync(torneoId))
                .ReturnsAsync(new List<Jugador> { jugadorExistente });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<JugadorYaInscriptoException>(() =>
                _equipoService.CreateEquipoAsync(torneoId, dto));

            Assert.Equal("22222222", ex.Dni);
            Assert.Contains("El jugador con DNI 22222222 ya se encuentra inscripto en otro equipo de este torneo (RN-02)", ex.Message);
            _equipoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Equipo>()), Times.Never);
        }

        [Fact]
        public async Task CreateEquipoAsync_WhenTeamNameExistsInTournament_ThrowsEquipoNombreDuplicadoException()
        {
            // Arrange - Ya existe un equipo con el mismo nombre en el torneo
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Futbol",
                Disciplina = Disciplina.Futbol,
                Estado = "Abierto para Inscripción"
            };

            var dto = new EquipoCreateDTO
            {
                Nombre = "Los Leones",
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "11111111" }
                }
            };

            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync(torneo);

            _equipoRepositoryMock.Setup(r => r.ExistsNombreInTorneoAsync(torneoId, dto.Nombre))
                .ReturnsAsync(true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<EquipoNombreDuplicadoException>(() =>
                _equipoService.CreateEquipoAsync(torneoId, dto));

            Assert.Equal("Ya existe un equipo con ese nombre en este torneo", ex.Message);
            _equipoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Equipo>()), Times.Never);
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
