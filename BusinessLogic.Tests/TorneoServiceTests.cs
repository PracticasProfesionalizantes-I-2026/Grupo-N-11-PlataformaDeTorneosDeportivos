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
    public class TorneoServiceTests
    {
        private readonly Mock<ITorneoRepository> _torneoRepositoryMock;
        private readonly Mock<ICuentaRecaudadoraService> _cuentaRecaudadoraMock;
        private readonly TorneoService _torneoService;

        public TorneoServiceTests()
        {
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _cuentaRecaudadoraMock = new Mock<ICuentaRecaudadoraService>();

            _cuentaRecaudadoraMock.Setup(c => c.IsCuentaActivaAsync())
                .ReturnsAsync(true);

            _torneoService = new TorneoService(
                _torneoRepositoryMock.Object,
                _cuentaRecaudadoraMock.Object);
        }

        [Fact]
        public async Task CreateAsync_EliminacionDirectaWithCupoMenorA4_ThrowsBusinessRuleConflictException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Test",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 3,
                CostoInscripcion = 100
            };

            // Act & Assert
            await Assert.ThrowsAsync<BusinessRuleConflictException>(() => _torneoService.CreateAsync(dto));
        }

        [Fact]
        public async Task CreateAsync_CostoInscripcionNegativo_ThrowsValidationException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Test",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                CostoInscripcion = -50
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _torneoService.CreateAsync(dto));
        }

        [Fact]
        public async Task CreateAsync_ValidData_ReturnsTorneoResponseDTO()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Valido",
                Disciplina = Disciplina.ESports,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 8,
                CostoInscripcion = 1000
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(It.IsAny<string>()))
                .ReturnsAsync((Torneo?)null);

            _torneoRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Torneo>()))
                .ReturnsAsync((Torneo t) => { t.Id = Guid.NewGuid(); return t; });

            // Act
            var result = await _torneoService.CreateAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(dto.Nombre, result.Nombre);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Once);
        }

        [Fact]
        public async Task CreateTorneoAsync_WithValidData_ReturnsCreatedTorneoDTO()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Copa de Campeones 2026",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 16,
                EsPago = true,
                CostoInscripcion = 2500m
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync((Torneo?)null);

            _torneoRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Torneo>()))
                .ReturnsAsync((Torneo t) => { t.Id = Guid.NewGuid(); return t; });

            // Act
            var result = await _torneoService.CreateTorneoAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Copa de Campeones 2026", result.Nombre);
            Assert.Equal("Futbol", result.Disciplina);
            Assert.Equal(16, result.CupoMaximo);
            Assert.True(result.EsPago);
            Assert.Equal(2500m, result.CostoInscripcion);
            Assert.Equal(EstadoTorneo.Abierto.ToString(), result.Estado);

            _torneoRepositoryMock.Verify(r => r.AddAsync(It.Is<Torneo>(t =>
                t.Nombre == "Copa de Campeones 2026" &&
                t.CupoMaximo == 16 &&
                t.Estado == EstadoTorneo.Abierto
            )), Times.Once);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenDisciplinaDoesNotExist_ThrowsDisciplinaNotFoundException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Disciplina Invalida",
                Disciplina = (Disciplina)999, // Inexistente
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<DisciplinaNotFoundException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("La disciplina especificada no existe", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenNameAlreadyExists_ThrowsTorneoNombreDuplicadoException()
        {
            // Arrange
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Duplicado",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                EsPago = false,
                CostoInscripcion = 0
            };

            var torneoExistente = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = "Torneo Duplicado"
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync(torneoExistente);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TorneoNombreDuplicadoException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("Ya existe un torneo con el nombre ingresado", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenEliminacionDirectaAndCupoNotPowerOfTwo_ThrowsCupoInvalidoEliminacionException()
        {
            // Arrange - RN-04: Cupo 6 no es potencia de 2
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Cupo Invalido",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 6, // Invalido: no es potencia de 2
                EsPago = false,
                CostoInscripcion = 0
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CupoInvalidoEliminacionException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenPagoAndCostoZeroOrNegative_ThrowsCostoInvalidoException()
        {
            // Arrange - RN-05: EsPago == true pero Costo <= 0
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Pago Sin Costo",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 0m // Invalido para torneo pago
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CostoInvalidoException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("El costo debe ser mayor a cero para torneos pagos", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task CreateTorneoAsync_WhenPagoAndCuentaRecaudadoraInactive_ThrowsCuentaRecaudadoraInactivaException()
        {
            // Arrange - RN-05: Cuenta recaudadora inactiva
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Sin Cuenta",
                Disciplina = Disciplina.ESports,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 1500m,
                CuentaRecaudadoraActiva = false
            };

            _torneoRepositoryMock.Setup(r => r.GetByNombreAsync(dto.Nombre))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<CuentaRecaudadoraInactivaException>(() =>
                _torneoService.CreateTorneoAsync(dto));

            Assert.Equal("No se puede publicar un torneo pago sin una cuenta recaudadora activa (RN-05)", ex.Message);
            _torneoRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Torneo>()), Times.Never);
        }

        [Fact]
        public async Task GetPosicionesAsync_CalculatesAndReturnsStandings()
        {
            // Arrange (CU-13 Flujo Principal)
            var torneoId = Guid.NewGuid();
            var equipoA = new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo A", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };
            var equipoB = new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo B", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };
            var equipoC = new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo C", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };
            var equipoD = new Equipo { Id = Guid.NewGuid(), Nombre = "Equipo D", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };

            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Liga Primavera",
                Modalidad = Modalidad.Liga,
                Estado = EstadoTorneo.EnDesarrollo,
                Equipos = new List<Equipo> { equipoA, equipoB, equipoC, equipoD },
                Partidos = new List<Partido>
                {
                    // Equipo A 3 - 1 Equipo B (A gana 3 pts, B 0 pts)
                    new Partido
                    {
                        Id = Guid.NewGuid(),
                        TorneoId = torneoId,
                        EquipoLocalId = equipoA.Id,
                        EquipoLocal = equipoA,
                        EquipoVisitanteId = equipoB.Id,
                        EquipoVisitante = equipoB,
                        MarcadorLocal = 3,
                        MarcadorVisitante = 1,
                        Estado = EstadoPartido.Finalizado
                    },
                    // Equipo C 2 - 2 Equipo D (C 1 pt, D 1 pt)
                    new Partido
                    {
                        Id = Guid.NewGuid(),
                        TorneoId = torneoId,
                        EquipoLocalId = equipoC.Id,
                        EquipoLocal = equipoC,
                        EquipoVisitanteId = equipoD.Id,
                        EquipoVisitante = equipoD,
                        MarcadorLocal = 2,
                        MarcadorVisitante = 2,
                        Estado = EstadoPartido.Finalizado
                    },
                    // Equipo A 1 - 0 Equipo C (A gana 3 pts -> 6 total, C 0 pts -> 1 total)
                    new Partido
                    {
                        Id = Guid.NewGuid(),
                        TorneoId = torneoId,
                        EquipoLocalId = equipoA.Id,
                        EquipoLocal = equipoA,
                        EquipoVisitanteId = equipoC.Id,
                        EquipoVisitante = equipoC,
                        MarcadorLocal = 1,
                        MarcadorVisitante = 0,
                        Estado = EstadoPartido.Finalizado
                    }
                }
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);

            // Act
            var result = (await _torneoService.GetPosicionesAsync(torneoId)).ToList();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(4, result.Count);

            // 1st: Equipo A (6 pts, 2 PJ, 2 PG, 0 PE, 0 PP, 4 GF, 1 GC, Dif +3)
            Assert.Equal(1, result[0].Posicion);
            Assert.Equal(equipoA.Id, result[0].EquipoId);
            Assert.Equal("Equipo A", result[0].EquipoNombre);
            Assert.Equal(6, result[0].Puntos);
            Assert.Equal(2, result[0].PartidosJugados);
            Assert.Equal(2, result[0].PartidosGanados);
            Assert.Equal(0, result[0].PartidosEmpatados);
            Assert.Equal(0, result[0].PartidosPerdidos);
            Assert.Equal(4, result[0].GolesAFavor);
            Assert.Equal(1, result[0].GolesEnContra);
            Assert.Equal(3, result[0].DiferenciaGoles);

            // Equipo C y D tienen 1 pt cada uno
            var posC = result.First(r => r.EquipoId == equipoC.Id);
            Assert.Equal(1, posC.Puntos);
            Assert.Equal(2, posC.PartidosJugados);
            Assert.Equal(2, posC.GolesAFavor);
            Assert.Equal(3, posC.GolesEnContra);
            Assert.Equal(-1, posC.DiferenciaGoles);

            var posD = result.First(r => r.EquipoId == equipoD.Id);
            Assert.Equal(1, posD.Puntos);
            Assert.Equal(1, posD.PartidosJugados);
            Assert.Equal(2, posD.GolesAFavor);
            Assert.Equal(2, posD.GolesEnContra);
            Assert.Equal(0, posD.DiferenciaGoles);

            // Equipo B tiene 0 pts
            var posB = result.First(r => r.EquipoId == equipoB.Id);
            Assert.Equal(0, posB.Puntos);
            Assert.Equal(1, posB.PartidosJugados);
            Assert.Equal(-2, posB.DiferenciaGoles);
        }

        [Fact]
        public async Task GetPosicionesAsync_WhenTorneoIsBrackets_ThrowsValidationException()
        {
            // Arrange (CU-13 Flujo Alternativo 3a)
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Brackets",
                Modalidad = Modalidad.EliminacionDirecta,
                Estado = EstadoTorneo.EnDesarrollo
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() =>
                _torneoService.GetPosicionesAsync(torneoId));
        }

        [Fact]
        public async Task GetPosicionesAsync_WhenTorneoNotFound_ThrowsNotFoundException()
        {
            // Arrange (CU-13 Flujo Alternativo 3b)
            var torneoId = Guid.NewGuid();
            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync((Torneo?)null);
            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _torneoService.GetPosicionesAsync(torneoId));
        }

        [Fact]
        public async Task GetPosicionesAsync_WithTieBreakers_SortsByDiferenciaGolesThenGolesAFavorThenNombre()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoAlpha = new Equipo { Id = Guid.NewGuid(), Nombre = "Alpha FC", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };
            var equipoBeta = new Equipo { Id = Guid.NewGuid(), Nombre = "Beta FC", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };
            var equipoGamma = new Equipo { Id = Guid.NewGuid(), Nombre = "Gamma FC", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };

            // Ambos ganan 1 partido (3 puntos cada uno)
            // Alpha: gana 5-3 (+2 dif, 5 GF)
            // Beta: gana 3-1 (+2 dif, 3 GF)
            // Gamma: pierde ambos
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Desempate",
                Modalidad = Modalidad.Liga,
                Estado = EstadoTorneo.EnDesarrollo,
                Equipos = new List<Equipo> { equipoAlpha, equipoBeta, equipoGamma },
                Partidos = new List<Partido>
                {
                    new Partido
                    {
                        Id = Guid.NewGuid(),
                        TorneoId = torneoId,
                        EquipoLocalId = equipoAlpha.Id,
                        EquipoLocal = equipoAlpha,
                        EquipoVisitanteId = equipoGamma.Id,
                        EquipoVisitante = equipoGamma,
                        MarcadorLocal = 5,
                        MarcadorVisitante = 3,
                        Estado = EstadoPartido.Finalizado
                    },
                    new Partido
                    {
                        Id = Guid.NewGuid(),
                        TorneoId = torneoId,
                        EquipoLocalId = equipoBeta.Id,
                        EquipoLocal = equipoBeta,
                        EquipoVisitanteId = equipoGamma.Id,
                        EquipoVisitante = equipoGamma,
                        MarcadorLocal = 3,
                        MarcadorVisitante = 1,
                        Estado = EstadoPartido.Finalizado
                    }
                }
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);

            // Act
            var result = (await _torneoService.GetPosicionesAsync(torneoId)).ToList();

            // Assert: Alpha y Beta tienen 3 pts y +2 dif, pero Alpha tiene 5 GF vs 3 GF de Beta
            Assert.Equal("Alpha FC", result[0].EquipoNombre);
            Assert.Equal("Beta FC", result[1].EquipoNombre);
            Assert.Equal("Gamma FC", result[2].EquipoNombre);
        }

        [Fact]
        public async Task GetLlavesAsync_CalculatesAndReturnsBrackets()
        {
            // Arrange (CU-13 Brackets Flujo Principal)
            var torneoId = Guid.NewGuid();
            var eq1 = new Equipo { Id = Guid.NewGuid(), Nombre = "Halcones", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };
            var eq2 = new Equipo { Id = Guid.NewGuid(), Nombre = "Águilas", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };
            var eq3 = new Equipo { Id = Guid.NewGuid(), Nombre = "Tigres", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };
            var eq4 = new Equipo { Id = Guid.NewGuid(), Nombre = "Leones", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneoId };

            var partidoSemi1 = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneoId,
                Ronda = 1,
                EquipoLocalId = eq1.Id,
                EquipoLocal = eq1,
                EquipoVisitanteId = eq2.Id,
                EquipoVisitante = eq2,
                MarcadorLocal = 2,
                MarcadorVisitante = 1,
                Estado = EstadoPartido.Finalizado
            };

            var partidoSemi2 = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneoId,
                Ronda = 1,
                EquipoLocalId = eq3.Id,
                EquipoLocal = eq3,
                EquipoVisitanteId = eq4.Id,
                EquipoVisitante = eq4,
                MarcadorLocal = 0,
                MarcadorVisitante = 1,
                Estado = EstadoPartido.Finalizado
            };

            var partidoFinal = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneoId,
                Ronda = 2,
                EquipoLocalId = eq1.Id,
                EquipoLocal = eq1,
                EquipoVisitanteId = eq4.Id,
                EquipoVisitante = eq4,
                Estado = EstadoPartido.Pendiente
            };

            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Copa Eliminatoria",
                Modalidad = Modalidad.EliminacionDirecta,
                Estado = EstadoTorneo.EnDesarrollo,
                Equipos = new List<Equipo> { eq1, eq2, eq3, eq4 },
                Partidos = new List<Partido> { partidoSemi1, partidoSemi2, partidoFinal }
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);

            // Act
            var result = await _torneoService.GetLlavesAsync(torneoId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(torneoId, result.TorneoId);
            Assert.Equal("Copa Eliminatoria", result.TorneoNombre);
            Assert.Equal(2, result.TotalRondas);
            Assert.Equal(2, result.Rondas.Count);

            var ronda1 = result.Rondas.First(r => r.NumeroRonda == 1);
            Assert.Equal("Semifinal", ronda1.NombreRonda);
            Assert.Equal(2, ronda1.Partidos.Count);
            Assert.Equal(eq1.Id, ronda1.Partidos[0].GanadorId);
            Assert.Equal("Halcones", ronda1.Partidos[0].GanadorNombre);
            Assert.Equal(eq4.Id, ronda1.Partidos[1].GanadorId);
            Assert.Equal("Leones", ronda1.Partidos[1].GanadorNombre);

            var ronda2 = result.Rondas.First(r => r.NumeroRonda == 2);
            Assert.Equal("Final", ronda2.NombreRonda);
            Assert.Single(ronda2.Partidos);
            Assert.Null(ronda2.Partidos[0].GanadorId);
        }

        [Fact]
        public async Task GetLlavesAsync_WhenTorneoIsLiga_ThrowsValidationException()
        {
            // Arrange (CU-13 Llaves con modalidad incompatible)
            var torneoId = Guid.NewGuid();
            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = "Torneo Regular",
                Modalidad = Modalidad.Liga,
                Estado = EstadoTorneo.EnDesarrollo
            };

            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync(torneo);

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() =>
                _torneoService.GetLlavesAsync(torneoId));
        }

        [Fact]
        public async Task GetLlavesAsync_WhenTorneoNotFound_ThrowsNotFoundException()
        {
            // Arrange (CU-13 Llaves torneo inexistente)
            var torneoId = Guid.NewGuid();
            _torneoRepositoryMock.Setup(r => r.GetTorneoConEquiposAsync(torneoId))
                .ReturnsAsync((Torneo?)null);
            _torneoRepositoryMock.Setup(r => r.GetByIdAsync(torneoId))
                .ReturnsAsync((Torneo?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _torneoService.GetLlavesAsync(torneoId));
        }
    }
}
