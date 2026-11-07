using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusinessLogic.Security;
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
    public class RedTeamFuzzingServiceTests
    {
        private readonly Mock<ITorneoRepository> _torneoRepoMock = new();
        private readonly Mock<IEquipoRepository> _equipoRepoMock = new();
        private readonly Mock<IJugadorRepository> _jugadorRepoMock = new();
        private readonly Mock<IPartidoRepository> _partidoRepoMock = new();
        private readonly Mock<IIncidenciaRepository> _incidenciaRepoMock = new();
        private readonly Mock<IUsuarioRepository> _usuarioRepoMock = new();
        private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
        private readonly Mock<ICuentaRecaudadoraService> _cuentaRecaudadoraMock = new();

        public RedTeamFuzzingServiceTests()
        {
            _cuentaRecaudadoraMock.Setup(c => c.IsCuentaActivaAsync()).ReturnsAsync(true);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(-5)]
        [InlineData(999)]
        public async Task Vector1_TorneoService_ModalidadFueraDeRango_ThrowsValidationException(int modalidadInvalida)
        {
            var service = new TorneoService(_torneoRepoMock.Object, _cuentaRecaudadoraMock.Object);
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Fuzz Modalidad",
                Disciplina = Disciplina.Futbol,
                Modalidad = (Modalidad)modalidadInvalida,
                CupoMaximo = 8
            };

            await Assert.ThrowsAsync<ValidationException>(() => service.CreateTorneoAsync(dto));
        }

        [Theory]
        [InlineData(99)]
        [InlineData(-1)]
        public async Task Vector1_UsuarioService_RolFueraDeRango_ThrowsValidationException(int rolInvalido)
        {
            var service = new UsuarioService(_usuarioRepoMock.Object, _passwordHasherMock.Object);
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Carlos",
                Apellido = "Perez",
                Dni = "40123456",
                Email = "carlos@test.com",
                Password = "Password123",
                Rol = (RolUsuario)rolInvalido
            };

            await Assert.ThrowsAsync<ValidationException>(() => service.RegisterUsuarioAsync(dto));
        }

        [Theory]
        [InlineData(999)]
        [InlineData(-10)]
        public async Task Vector1_IncidenciaService_TipoIncidenciaFueraDeRango_ThrowsValidationException(int tipoInvalido)
        {
            var service = new IncidenciaService(
                _incidenciaRepoMock.Object,
                _partidoRepoMock.Object,
                _jugadorRepoMock.Object,
                _equipoRepoMock.Object);

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = (TipoIncidencia)tipoInvalido,
                Minuto = 15,
                JugadorId = Guid.NewGuid(),
                EquipoId = Guid.NewGuid()
            };

            await Assert.ThrowsAsync<ValidationException>(() => service.RegistrarIncidenciaAsync(Guid.NewGuid(), dto));
        }

        [Theory]
        [InlineData("-40123456")]
        [InlineData("40.123.456")]
        [InlineData("4012345A")]
        [InlineData("123")]
        [InlineData("12345678901234567890")]
        public async Task Vector2_DniMaliciosoEnServicios_ThrowsValidationException(string dniInvalido)
        {
            var usuarioService = new UsuarioService(_usuarioRepoMock.Object, _passwordHasherMock.Object);
            var jugadorService = new JugadorService(_jugadorRepoMock.Object, _equipoRepoMock.Object);
            var veedorService = new VeedorService(_usuarioRepoMock.Object, _passwordHasherMock.Object, _partidoRepoMock.Object);

            await Assert.ThrowsAsync<ValidationException>(() => usuarioService.RegisterUsuarioAsync(new UsuarioRegistroDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = dniInvalido,
                Email = "juan@test.com",
                Password = "Password123",
                Rol = RolUsuario.Capitan
            }));

            await Assert.ThrowsAsync<ValidationException>(() => jugadorService.CreateAsync(new JugadorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = dniInvalido,
                EquipoId = Guid.NewGuid()
            }));

            await Assert.ThrowsAsync<ValidationException>(() => veedorService.CreateVeedorAsync(new VeedorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = dniInvalido,
                Email = "veedor@test.com"
            }));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-16)]
        [InlineData(1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public async Task Vector3_TorneoService_CupoMaximoExtremo_ThrowsValidationException(int cupoExtremo)
        {
            var service = new TorneoService(_torneoRepoMock.Object, _cuentaRecaudadoraMock.Object);
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Cupo Extremo",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = cupoExtremo
            };

            await Assert.ThrowsAsync<ValidationException>(() => service.CreateTorneoAsync(dto));
        }

        [Fact]
        public async Task Vector4_EquipoService_ListaJugadoresConElementoNull_ThrowsValidationExceptionSinNullRef()
        {
            var service = new EquipoService(_equipoRepoMock.Object, _torneoRepoMock.Object, _jugadorRepoMock.Object);
            var dto = new EquipoCreateDTO
            {
                TorneoId = Guid.NewGuid(),
                Nombre = "Equipo Con Null",
                Jugadores = new List<JugadorCreateDTO> { null! }
            };

            await Assert.ThrowsAsync<ValidationException>(() => service.CreateEquipoAsync(dto.TorneoId, dto));
        }

        [Fact]
        public async Task Vector5_FixtureService_ParametrosInvalidos_ThrowsValidationException()
        {
            var service = new FixtureService(_torneoRepoMock.Object, _partidoRepoMock.Object);
            var torneoId = Guid.NewGuid();

            // FechaInicio default (0001-01-01)
            await Assert.ThrowsAsync<ValidationException>(() => service.GenerarFixtureAsync(torneoId, new FixtureParametrosDTO
            {
                FechaInicio = default,
                FranjasHorarias = new List<string> { "14:00" },
                CanchasDisponibles = new List<string> { "Cancha 1" }
            }));

            // Horario gigante ("10000000") que antes desbordaba DateTime.Add(TimeSpan)
            await Assert.ThrowsAsync<ValidationException>(() => service.GenerarFixtureAsync(torneoId, new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(5),
                FranjasHorarias = new List<string> { "10000000" },
                CanchasDisponibles = new List<string> { "Cancha 1" }
            }));

            // Canchas duplicadas
            await Assert.ThrowsAsync<ValidationException>(() => service.GenerarFixtureAsync(torneoId, new FixtureParametrosDTO
            {
                FechaInicio = DateTime.Today.AddDays(5),
                FranjasHorarias = new List<string> { "14:00" },
                CanchasDisponibles = new List<string> { "Cancha 1", "cancha 1" }
            }));
        }

        [Fact]
        public async Task Vector6_TorneoService_EsPagoFalseConCostoPositivo_ThrowsCostoInvalidoException()
        {
            var service = new TorneoService(_torneoRepoMock.Object, _cuentaRecaudadoraMock.Object);
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Gratis Con Costo",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 500m
            };

            var ex = await Assert.ThrowsAsync<CostoInvalidoException>(() => service.CreateTorneoAsync(dto));
            Assert.Equal("No se puede ingresar un costo de inscripción si el torneo es gratuito (EsPago = false)", ex.Message);
        }

        [Fact]
        public async Task Vector7_TorneoService_DisciplinaInvalidaConDisciplinaIdValido_ThrowsDisciplinaNotFoundException()
        {
            var service = new TorneoService(_torneoRepoMock.Object, _cuentaRecaudadoraMock.Object);
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Bruno Ambas Propiedades",
                Disciplina = (Disciplina)99,
                DisciplinaId = 1,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0m
            };

            await Assert.ThrowsAsync<DisciplinaNotFoundException>(() => service.CreateTorneoAsync(dto));
        }

        [Theory]
        [InlineData("hola123")]
        [InlineData("@@@")]
        [InlineData("12")]
        [InlineData("123456789012345678")]
        [InlineData("   ")]
        public async Task Vector8_TelefonoInvalidoEnTodosLosModulos_ThrowsValidationException(string telefonoInvalido)
        {
            var usuarioService = new UsuarioService(_usuarioRepoMock.Object, _passwordHasherMock.Object);
            var jugadorService = new JugadorService(_jugadorRepoMock.Object, _equipoRepoMock.Object);
            var veedorService = new VeedorService(_usuarioRepoMock.Object, _passwordHasherMock.Object, _partidoRepoMock.Object);
            var equipoService = new EquipoService(_equipoRepoMock.Object, _torneoRepoMock.Object, _jugadorRepoMock.Object);

            await Assert.ThrowsAsync<ValidationException>(() => usuarioService.RegisterUsuarioAsync(new UsuarioRegistroDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                Email = "juan@test.com",
                Password = "Password123",
                Telefono = telefonoInvalido,
                Rol = RolUsuario.Capitan
            }));

            await Assert.ThrowsAsync<ValidationException>(() => jugadorService.CreateAsync(new JugadorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                Telefono = telefonoInvalido,
                EquipoId = Guid.NewGuid()
            }));

            await Assert.ThrowsAsync<ValidationException>(() => veedorService.CreateVeedorAsync(new VeedorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                Email = "veedor@test.com",
                Telefono = telefonoInvalido
            }));

            await Assert.ThrowsAsync<ValidationException>(() => veedorService.UpdateVeedorAsync(Guid.NewGuid(), new VeedorUpdateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Telefono = telefonoInvalido
            }));

            await Assert.ThrowsAsync<ValidationException>(() => equipoService.CreateEquipoAsync(Guid.NewGuid(), new EquipoCreateDTO
            {
                TorneoId = Guid.NewGuid(),
                Nombre = "Equipo Valido",
                Telefono = telefonoInvalido,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "40123456" }
                }
            }));
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("@@@")]
        [InlineData("A")]
        [InlineData("Juan123")]
        public async Task Vector9_NombreYApellidoConNumerosOSimbolos_ThrowsValidationException(string textoInvalido)
        {
            var usuarioService = new UsuarioService(_usuarioRepoMock.Object, _passwordHasherMock.Object);
            var jugadorService = new JugadorService(_jugadorRepoMock.Object, _equipoRepoMock.Object);
            var veedorService = new VeedorService(_usuarioRepoMock.Object, _passwordHasherMock.Object, _partidoRepoMock.Object);

            await Assert.ThrowsAsync<ValidationException>(() => usuarioService.RegisterUsuarioAsync(new UsuarioRegistroDTO
            {
                Nombre = textoInvalido,
                Apellido = "Perez",
                Dni = "40123456",
                Email = "juan@test.com",
                Password = "Password123",
                Rol = RolUsuario.Capitan
            }));

            await Assert.ThrowsAsync<ValidationException>(() => jugadorService.CreateAsync(new JugadorCreateDTO
            {
                Nombre = "Juan",
                Apellido = textoInvalido,
                Dni = "40123456",
                EquipoId = Guid.NewGuid()
            }));

            await Assert.ThrowsAsync<ValidationException>(() => veedorService.CreateVeedorAsync(new VeedorCreateDTO
            {
                Nombre = textoInvalido,
                Apellido = "Perez",
                Dni = "40123456",
                Email = "veedor@test.com"
            }));
        }

        [Fact]
        public async Task Vector10_JugadorPropiedadesAdicionalesInvalidas_ThrowsValidationException()
        {
            var jugadorService = new JugadorService(_jugadorRepoMock.Object, _equipoRepoMock.Object);

            // Dorsal fuera de rango (0 o 100)
            await Assert.ThrowsAsync<ValidationException>(() => jugadorService.CreateAsync(new JugadorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                NumeroCamiseta = 100,
                EquipoId = Guid.NewGuid()
            }));

            // Edad ilógica (3 años o 95 años)
            await Assert.ThrowsAsync<ValidationException>(() => jugadorService.CreateAsync(new JugadorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                Edad = 3,
                EquipoId = Guid.NewGuid()
            }));

            // FechaNacimiento en el futuro
            await Assert.ThrowsAsync<ValidationException>(() => jugadorService.CreateAsync(new JugadorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                FechaNacimiento = DateTime.Today.AddDays(1),
                EquipoId = Guid.NewGuid()
            }));
        }

        [Fact]
        public async Task CasoA_CrearTorneo_EsPagoFalseYCosto1000_ThrowsCostoInvalidoException()
        {
            var service = new TorneoService(_torneoRepoMock.Object, _cuentaRecaudadoraMock.Object);
            var dto = new TorneoCreateDTO
            {
                Nombre = "Torneo Gratuito Con Costo 1000",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 1000m
            };

            await Assert.ThrowsAsync<CostoInvalidoException>(() => service.CreateTorneoAsync(dto));
        }

        [Fact]
        public async Task CasoB_CrearTorneo_Disciplina99YModalidad16_FallaValidacion()
        {
            var service = new TorneoService(_torneoRepoMock.Object, _cuentaRecaudadoraMock.Object);

            // Disciplina 99 -> DisciplinaNotFoundException (404)
            await Assert.ThrowsAsync<DisciplinaNotFoundException>(() => service.CreateTorneoAsync(new TorneoCreateDTO
            {
                Nombre = "Torneo Disciplina 99",
                Disciplina = (Disciplina)99,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            }));

            // Modalidad 16 -> ValidationException (400)
            await Assert.ThrowsAsync<ValidationException>(() => service.CreateTorneoAsync(new TorneoCreateDTO
            {
                Nombre = "Torneo Modalidad 16",
                Disciplina = Disciplina.Futbol,
                Modalidad = (Modalidad)16,
                CupoMaximo = 8
            }));
        }

        [Fact]
        public async Task CasoC_RegistrarJugadorYUsuario_TelefonoTextoInvalidoAbc_ThrowsValidationException()
        {
            var usuarioService = new UsuarioService(_usuarioRepoMock.Object, _passwordHasherMock.Object);
            var jugadorService = new JugadorService(_jugadorRepoMock.Object, _equipoRepoMock.Object);

            await Assert.ThrowsAsync<ValidationException>(() => usuarioService.RegisterUsuarioAsync(new UsuarioRegistroDTO
            {
                Nombre = "Carlos",
                Apellido = "Gomez",
                Dni = "40123456",
                Email = "carlos@test.com",
                Password = "Password123!",
                Telefono = " texto_invalido_abc ",
                Rol = RolUsuario.Capitan
            }));

            await Assert.ThrowsAsync<ValidationException>(() => jugadorService.CreateAsync(new JugadorCreateDTO
            {
                Nombre = "Carlos",
                Apellido = "Gomez",
                Dni = "40123456",
                Telefono = " texto_invalido_abc ",
                EquipoId = Guid.NewGuid()
            }));
        }

        [Fact]
        public async Task CasoD_RegistrarJugadorYUsuario_NombreConNumerosODniFueraDeRango_ThrowsValidationException()
        {
            var usuarioService = new UsuarioService(_usuarioRepoMock.Object, _passwordHasherMock.Object);
            var jugadorService = new JugadorService(_jugadorRepoMock.Object, _equipoRepoMock.Object);

            // Nombre con números
            await Assert.ThrowsAsync<ValidationException>(() => usuarioService.RegisterUsuarioAsync(new UsuarioRegistroDTO
            {
                Nombre = "Carlos99",
                Apellido = "Gomez",
                Dni = "40123456",
                Email = "carlos@test.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            }));

            await Assert.ThrowsAsync<ValidationException>(() => jugadorService.CreateAsync(new JugadorCreateDTO
            {
                Nombre = "Carlos99",
                Apellido = "Gomez",
                Dni = "40123456",
                EquipoId = Guid.NewGuid()
            }));

            // DNI fuera de rango (muy corto o muy largo)
            await Assert.ThrowsAsync<ValidationException>(() => usuarioService.RegisterUsuarioAsync(new UsuarioRegistroDTO
            {
                Nombre = "Carlos",
                Apellido = "Gomez",
                Dni = "12345",
                Email = "carlos@test.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            }));

            await Assert.ThrowsAsync<ValidationException>(() => jugadorService.CreateAsync(new JugadorCreateDTO
            {
                Nombre = "Carlos",
                Apellido = "Gomez",
                Dni = "1234567890",
                EquipoId = Guid.NewGuid()
            }));
        }

        [Fact]
        public async Task Vector11_ModulosRestantes_InscripcionFixturePartidoIncidenciaPublicidad_ThrowsValidationException()
        {
            var inscripcionService = new InscripcionService(_torneoRepoMock.Object, _equipoRepoMock.Object);
            var fixtureService = new FixtureService(_torneoRepoMock.Object, _partidoRepoMock.Object);
            var partidoService = new PartidoService(_partidoRepoMock.Object);
            var incidenciaService = new IncidenciaService(_incidenciaRepoMock.Object, _partidoRepoMock.Object, _jugadorRepoMock.Object, _equipoRepoMock.Object);
            var publicidadRepoMock = new Mock<IPublicidadRepository>();
            var publicidadService = new PublicidadService(publicidadRepoMock.Object, _torneoRepoMock.Object);

            // 1. Inscripcion: motivo solo espacios o menor a 3 caracteres
            await Assert.ThrowsAsync<ValidationException>(() =>
                inscripcionService.RechazarInscripcionAsync(Guid.NewGuid(), Guid.NewGuid(), new RechazarInscripcionDTO { MotivoRechazo = "   " }));

            // 2. Fixture: FechaFin anterior a FechaInicio, DuracionPartidoMinutos inválida, HoraFin <= HoraInicio
            await Assert.ThrowsAsync<ValidationException>(() =>
                fixtureService.GenerarFixtureAsync(Guid.NewGuid(), new FixtureParametrosDTO
                {
                    FechaInicio = DateTime.Today.AddDays(10),
                    FechaFin = DateTime.Today.AddDays(5),
                    FranjasHorarias = new List<string> { "14:00" },
                    CanchasDisponibles = new List<string> { "Cancha 1" }
                }));

            await Assert.ThrowsAsync<ValidationException>(() =>
                fixtureService.GenerarFixtureAsync(Guid.NewGuid(), new FixtureParametrosDTO
                {
                    FechaInicio = DateTime.Today.AddDays(5),
                    DuracionPartidoMinutos = 5, // menor a 10
                    FranjasHorarias = new List<string> { "14:00" },
                    CanchasDisponibles = new List<string> { "Cancha 1" }
                }));

            await Assert.ThrowsAsync<ValidationException>(() =>
                fixtureService.GenerarFixtureAsync(Guid.NewGuid(), new FixtureParametrosDTO
                {
                    FechaInicio = DateTime.Today.AddDays(5),
                    HoraInicio = "18:00",
                    HoraFin = "14:00",
                    FranjasHorarias = new List<string> { "14:00" },
                    CanchasDisponibles = new List<string> { "Cancha 1" }
                }));

            // 3. Partido: goles > 999 o penales negativos
            await Assert.ThrowsAsync<ValidationException>(() =>
                partidoService.LoadResultadoAsync(Guid.NewGuid(), new CargarResultadoDTO { GolesLocal = 1000, GolesVisitante = 0 }));

            // 4. Incidencia: Observacion > 300 caracteres
            await Assert.ThrowsAsync<ValidationException>(() =>
                incidenciaService.RegistrarIncidenciaAsync(Guid.NewGuid(), new IncidenciaCreateDTO
                {
                    TipoIncidencia = TipoIncidencia.Gol,
                    Minuto = 10,
                    JugadorId = Guid.NewGuid(),
                    EquipoId = Guid.NewGuid(),
                    Observacion = new string('A', 301)
                }));

            // 5. Publicidad: URL inválida y Guid.Empty
            await Assert.ThrowsAsync<ValidationException>(() => publicidadService.RegisterClickAsync(Guid.Empty));
            await Assert.ThrowsAsync<ValidationException>(() => publicidadService.CreateAsync(new PublicidadCreateDTO
            {
                Titulo = "Banner Sponsor",
                NombrePatrocinador = "Sponsor SA",
                ImagenUrl = "no_es_una_url",
                UrlDestino = "https://sponsor.com"
            }));
        }

        [Fact]
        public void TorneoCreateDTO_CupoMaximoInvalido_FallaValidacion()
        {
            var dtoBajo = new TorneoCreateDTO
            {
                Nombre = "Torneo Cupo Bajo",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 1,
                EsPago = false,
                CostoInscripcion = 0
            };

            var contextBajo = new System.ComponentModel.DataAnnotations.ValidationContext(dtoBajo);
            var resultsBajo = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var isValidBajo = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(dtoBajo, contextBajo, resultsBajo, true);
            Assert.False(isValidBajo);

            var dtoAlto = new TorneoCreateDTO
            {
                Nombre = "Torneo Cupo Alto",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 200,
                EsPago = false,
                CostoInscripcion = 0
            };

            var contextAlto = new System.ComponentModel.DataAnnotations.ValidationContext(dtoAlto);
            var resultsAlto = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var isValidAlto = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(dtoAlto, contextAlto, resultsAlto, true);
            Assert.False(isValidAlto);
        }

        [Fact]
        public void TorneoUpdateDTO_PropiedadesExtendidas_FallaValidacionCuandoSonInvalidas()
        {
            var dto = new TorneoUpdateDTO
            {
                Nombre = "Torneo Valido",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0,
                Descripcion = new string('A', 1001), // Excede 1000
                Reglamento = "<script>alert('xss')</script>", // Contiene HTML
                FechaInicio = DateTime.Today.AddDays(-1), // En el pasado
                FechaFin = DateTime.Today.AddDays(-5) // Fin antes de inicio
            };

            var context = new System.ComponentModel.DataAnnotations.ValidationContext(dto);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(dto, context, results, true);
            Assert.False(isValid);
            Assert.True(results.Count >= 3);
        }
    }
}
