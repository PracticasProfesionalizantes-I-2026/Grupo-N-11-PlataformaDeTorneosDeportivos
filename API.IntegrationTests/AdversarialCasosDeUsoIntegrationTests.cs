using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using DataAccess.Data;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    public class AdversarialCasosDeUsoIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public AdversarialCasosDeUsoIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        #region CU-01: Registrarse en la plataforma (Pruebas con datos erróneos)

        [Fact]
        public async Task CU01_Registro_EmailFormatoInvalido_Retorna400BadRequest()
        {
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "33111222",
                Email = "email-sin-arroba-ni-dominio",
                Password = "Password123",
                Rol = RolUsuario.Capitan
            };

            var res = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CU01_Registro_PasswordInsegura_Retorna400BadRequest()
        {
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "33111223",
                Email = "juan.debil@test.com",
                Password = "debil", // menos de 8 caracteres y sin dígitos
                Rol = RolUsuario.Capitan
            };

            var res = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CU01_Registro_CamposRequeridosVacios_Retorna400BadRequest()
        {
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "",
                Apellido = "",
                Dni = "",
                Email = "",
                Password = ""
            };

            var res = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CU01_Registro_DniConLetras_Retorna400BadRequest()
        {
            var dto = new UsuarioRegistroDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "3311122A",
                Email = "juan.dniinvalido@test.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            var res = await _client.PostAsJsonAsync("/api/usuarios/registro", dto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        #endregion

        #region CU-02: Iniciar sesión (Pruebas con datos erróneos)

        [Fact]
        public async Task CU02_Login_UsuarioInexistente_Retorna401Unauthorized()
        {
            var dto = new LoginRequestDTO
            {
                EmailOrDni = "usuario_inexistente_99999@test.com",
                Password = "CualquierPassword123"
            };

            var res = await _client.PostAsJsonAsync("/api/auth/login", dto);

            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        [Fact]
        public async Task CU02_Login_PasswordErronea_Retorna401Unauthorized()
        {
            // Creamos usuario
            var email = $"login_fail_{Guid.NewGuid():N}@test.com";
            var regDto = new UsuarioRegistroDTO
            {
                Nombre = "Login",
                Apellido = "Erroneo",
                Dni = $"{new Random().Next(10000000, 99999999)}",
                Email = email,
                Password = "PasswordCorrecto123",
                Rol = RolUsuario.Capitan
            };
            await _client.PostAsJsonAsync("/api/usuarios/registro", regDto);

            // Intentamos con password incorrecta
            var loginDto = new LoginRequestDTO
            {
                EmailOrDni = email,
                Password = "PasswordIncorrecta999"
            };

            var res = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        [Fact]
        public async Task CU02_Login_CuentaInactiva_Retorna403Forbidden()
        {
            var email = $"inactivo_{Guid.NewGuid():N}@test.com";
            var dni = $"{new Random().Next(10000000, 99999999)}";
            var regDto = new UsuarioRegistroDTO
            {
                Nombre = "Inactivo",
                Apellido = "Usuario",
                Dni = dni,
                Email = email,
                Password = "Password123",
                Rol = RolUsuario.Capitan
            };
            await _client.PostAsJsonAsync("/api/usuarios/registro", regDto);

            // Inactivamos en BD
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var u = await db.Usuarios.FindAsync((await db.Usuarios.FirstOrDefaultAsync(x => x.Email == email))!.Id);
                u!.Activo = false;
                await db.SaveChangesAsync();
            }

            var loginDto = new LoginRequestDTO
            {
                EmailOrDni = email,
                Password = "Password123"
            };

            var res = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        #endregion

        #region CU-03: Recuperar y Resetear contraseña (Pruebas con datos erróneos)

        [Fact]
        public async Task CU03_ResetPassword_TokenInvalidoOInexistente_Retorna400BadRequest()
        {
            var dto = new ResetPasswordRequestDTO
            {
                Token = "token_totalmente_inventado_y_falso",
                NewPassword = "NuevaPassword123"
            };

            var res = await _client.PostAsJsonAsync("/api/auth/reset-password", dto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CU03_ResetPassword_PasswordDebil_Retorna400BadRequest()
        {
            var dto = new ResetPasswordRequestDTO
            {
                Token = "un_token_cualquiera",
                NewPassword = "123" // débil
            };

            var res = await _client.PostAsJsonAsync("/api/auth/reset-password", dto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        #endregion

        #region CU-04: Crear Torneo (Pruebas con datos erróneos y reglas de negocio)

        [Theory]
        [InlineData(5)] // no potencia de 2
        [InlineData(6)] // no potencia de 2
        [InlineData(7)] // no potencia de 2
        [InlineData(9)] // no potencia de 2
        public async Task CU04_CreateTorneo_EliminacionDirecta_CupoNoPotenciaDe2_Retorna422(int cupoInvalido)
        {
            var dto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Invalido {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = cupoInvalido,
                EsPago = false,
                CostoInscripcion = 0
            };

            var res = await _client.PostAsJsonAsync("/api/torneos", dto);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        }

        [Fact]
        public async Task CU04_CreateTorneo_EliminacionDirecta_CupoMenorA4_RetornaError()
        {
            var dto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Cupo2 {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 2, // < 4
                EsPago = false,
                CostoInscripcion = 0
            };

            var res = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Debe rechazar por ser < 4 (Conflict 409 o UnprocessableEntity 422)
            Assert.True(res.StatusCode == HttpStatusCode.Conflict || res.StatusCode == HttpStatusCode.UnprocessableEntity);
        }

        [Fact]
        public async Task CU04_CreateTorneo_PagoConCostoCero_Retorna400BadRequest()
        {
            var dto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Pago Costo 0 {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 0, // Inconsistente con EsPago
                CuentaRecaudadoraActiva = true
            };

            var res = await _client.PostAsJsonAsync("/api/torneos", dto);

            // Validado con CostoInvalidoException : ValidationException (400 Bad Request)
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CU04_CreateTorneo_PagoSinCuentaRecaudadoraActiva_Retorna422UnprocessableEntity()
        {
            var dto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Sin Cuenta {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 1500,
                CuentaRecaudadoraActiva = false // Cuenta inactiva
            };

            var res = await _client.PostAsJsonAsync("/api/torneos", dto);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        }

        #endregion

        #region CU-05: Registrar Equipos y Lista de Jugadores (Pruebas con datos erróneos)

        [Fact]
        public async Task CU05_CreateEquipo_TorneoInexistente_Retorna404NotFound()
        {
            var dto = new EquipoCreateDTO
            {
                TorneoId = Guid.NewGuid(),
                Nombre = "Equipo Fantasma",
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "11111111" }
                }
            };

            var res = await _client.PostAsJsonAsync($"/api/torneos/{dto.TorneoId}/equipos", dto);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task CU05_CreateEquipo_MenosJugadoresDelMinimoReglamentario_Retorna422UnprocessableEntity()
        {
            // Creamos torneo de Fútbol (mínimo 5 jugadores)
            var torneoDto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Futbol Min {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };
            var tRes = await _client.PostAsJsonAsync("/api/torneos", torneoDto);
            var torneo = await tRes.Content.ReadFromJsonAsync<TorneoResponseDTO>();

            // Enviamos solo 2 jugadores
            var equipoDto = new EquipoCreateDTO
            {
                TorneoId = torneo!.Id,
                Nombre = "Equipo Incompleto",
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Uno", Dni = "40111222" },
                    new() { Nombre = "Carlos", Apellido = "Dos", Dni = "40111223" }
                }
            };

            var res = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", equipoDto);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        }

        [Fact]
        public async Task CU05_CreateEquipo_DnisDuplicadosEnMismaLista_Retorna400BadRequest()
        {
            var torneoDto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Futbol DniDup {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };
            var tRes = await _client.PostAsJsonAsync("/api/torneos", torneoDto);
            var torneo = await tRes.Content.ReadFromJsonAsync<TorneoResponseDTO>();

            // 5 jugadores pero dos con el mismo DNI "40999999"
            var equipoDto = new EquipoCreateDTO
            {
                TorneoId = torneo!.Id,
                Nombre = "Equipo DNIs Repetidos",
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "40999999" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = "40999999" }, // DUPLICADO INTERNO
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = "40999991" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = "40999992" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = "40999993" }
                }
            };

            var res = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", equipoDto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CU05_CreateEquipo_JugadorEnDosEquiposDelMismoTorneo_Retorna409Conflict_RN02()
        {
            var torneoDto = new TorneoCreateDTO
            {
                Nombre = $"Torneo RN02 {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };
            var tRes = await _client.PostAsJsonAsync("/api/torneos", torneoDto);
            var torneo = await tRes.Content.ReadFromJsonAsync<TorneoResponseDTO>();

            var dniCompartido = $"{new Random().Next(10000000, 99999999)}";

            // Equipo 1
            var eq1 = new EquipoCreateDTO
            {
                TorneoId = torneo!.Id,
                Nombre = "Equipo Alfa",
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = dniCompartido },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = $"{new Random().Next(10000000, 99999999)}" }
                }
            };
            var r1 = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", eq1);
            Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

            // Equipo 2 intenta registrar al mismo jugador con dniCompartido en el mismo torneo
            var eq2 = new EquipoCreateDTO
            {
                TorneoId = torneo.Id,
                Nombre = "Equipo Beta",
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Pedro", Apellido = "Perez", Dni = dniCompartido }, // VIOLA RN-02
                    new() { Nombre = "Pablo", Apellido = "Lopez", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "Mateo", Apellido = "Gomez", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "Tomas", Apellido = "Diaz", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "Nicolas", Apellido = "Romero", Dni = $"{new Random().Next(10000000, 99999999)}" }
                }
            };
            var r2 = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", eq2);

            Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
        }

        #endregion

        #region CU-06: Dictaminar Inscripción (Pruebas con datos erróneos)

        [Fact]
        public async Task CU06_RechazarInscripcion_SinMotivo_Retorna400BadRequest()
        {
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            var dto = new RechazarInscripcionDTO
            {
                MotivoRechazo = "" // Vacío
            };

            var res = await _client.PatchAsJsonAsync($"/api/torneos/{torneoId}/equipos/{equipoId}/rechazar", dto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CU06_AprobarInscripcion_TorneoOEquipoInexistente_Retorna404NotFound()
        {
            var res = await _client.PatchAsync($"/api/torneos/{Guid.NewGuid()}/equipos/{Guid.NewGuid()}/aprobar", null);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        #endregion

        #region CU-08 / CU-16: Generar Fixture (Pruebas con datos erróneos)

        [Fact]
        public async Task CU08_GenerarFixture_TorneoInexistente_Retorna404NotFound()
        {
            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(1),
                Horarios = new List<string> { "10:00" },
                Sedes = new List<string> { "Cancha 1" }
            };
            var res = await _client.PostAsJsonAsync($"/api/torneos/{Guid.NewGuid()}/generar-fixture", dto);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task CU08_GenerarFixture_SinCupoMinimoConfirmados_Retorna409Conflict()
        {
            var torneoDto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Sin Equipos {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };
            var tRes = await _client.PostAsJsonAsync("/api/torneos", torneoDto);
            var torneo = await tRes.Content.ReadFromJsonAsync<TorneoResponseDTO>();

            var dto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(1),
                Horarios = new List<string> { "10:00" },
                Sedes = new List<string> { "Cancha 1" }
            };
            var res = await _client.PostAsJsonAsync($"/api/torneos/{torneo!.Id}/generar-fixture", dto);

            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }

        #endregion

        #region CU-10: Cargar Resultados (Pruebas con datos erróneos)

        [Fact]
        public async Task CU10_CargarResultado_PartidoInexistente_Retorna404NotFound()
        {
            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = 2,
                MarcadorVisitante = 1
            };

            var res = await _client.PutAsJsonAsync($"/api/partidos/{Guid.NewGuid()}/resultado", dto);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task CU10_CargarResultado_MarcadorNegativo_Retorna400BadRequest()
        {
            // Creamos un partido real
            var partidoId = Guid.NewGuid();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var t = new Torneo
                {
                    Id = Guid.NewGuid(),
                    Nombre = $"T {Guid.NewGuid():N}",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 4,
                    Estado = EstadoTorneo.EnDesarrollo
                };
                db.Torneos.Add(t);
                var p = new Partido
                {
                    Id = partidoId,
                    TorneoId = t.Id,
                    FechaHora = DateTime.UtcNow,
                    Sede = "Cancha 1",
                    Ronda = 1,
                    Estado = EstadoPartido.Pendiente
                };
                db.Partidos.Add(p);
                await db.SaveChangesAsync();
            }

            var dto = new CargarResultadoDTO
            {
                MarcadorLocal = -1, // NEGATIVO
                MarcadorVisitante = 2
            };

            var res = await _client.PutAsJsonAsync($"/api/partidos/{partidoId}/resultado", dto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        #endregion

        #region CU-11: Registrar Incidencias (Pruebas con datos erróneos)

        [Fact]
        public async Task CU11_RegistrarIncidencia_PartidoInexistente_Retorna404NotFound()
        {
            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = 15,
                JugadorId = Guid.NewGuid(),
                EquipoId = Guid.NewGuid()
            };

            var res = await _client.PostAsJsonAsync($"/api/partidos/{Guid.NewGuid()}/incidencias", dto);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task CU11_RegistrarIncidencia_MinutoInvalido_Retorna400BadRequest()
        {
            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = -5, // Minuto negativo
                JugadorId = Guid.NewGuid(),
                EquipoId = Guid.NewGuid()
            };

            var res = await _client.PostAsJsonAsync($"/api/partidos/{Guid.NewGuid()}/incidencias", dto);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        #endregion

        #region CU-13: Tablas y Llaves (Pruebas con datos erróneos)

        [Fact]
        public async Task CU13_GetPosiciones_TorneoInexistente_Retorna404NotFound()
        {
            var res = await _client.GetAsync($"/api/torneos/{Guid.NewGuid()}/posiciones");

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task CU13_GetPosiciones_TorneoEliminacionDirecta_Retorna400BadRequest()
        {
            var torneoDto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Brackets {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = 8
            };
            var tRes = await _client.PostAsJsonAsync("/api/torneos", torneoDto);
            var torneo = await tRes.Content.ReadFromJsonAsync<TorneoResponseDTO>();

            var res = await _client.GetAsync($"/api/torneos/{torneo!.Id}/posiciones");

            // Tabla de posiciones es incompatible con eliminación directa (400)
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        #endregion

        #region CU-14: Estadísticas (Pruebas con datos erróneos)

        [Fact]
        public async Task CU14_GetEstadisticasJugador_Inexistente_Retorna404NotFound()
        {
            var res = await _client.GetAsync($"/api/jugadores/{Guid.NewGuid()}/estadisticas");

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task CU14_GetEstadisticasEquipo_Inexistente_Retorna404NotFound()
        {
            var res = await _client.GetAsync($"/api/equipos/{Guid.NewGuid()}/estadisticas");

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        #endregion

        #region CU-15: Publicidad (Pruebas con datos erróneos)

        [Fact]
        public async Task CU15_GetPublicidad_TorneoIdCualquiera_Retorna200OKYBannersGlobales()
        {
            var res = await _client.GetAsync($"/api/torneos/{Guid.NewGuid()}/publicidad");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var banners = await res.Content.ReadFromJsonAsync<List<PublicidadResponseDTO>>();
            Assert.NotNull(banners);
        }

        [Fact]
        public async Task CU15_ClickPublicidad_BannerInexistente_Retorna404NotFound()
        {
            var res = await _client.PostAsync($"/api/publicidad/{Guid.NewGuid()}/click", null);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        #endregion

        #region Suite de Pruebas: Ataque del Profesor (Red Team / Fuzzing)

        [Theory]
        [InlineData("{ \"nombre\": \"Torneo Roto\", \"cupoMaximo\": ")]
        [InlineData("{ \"nombre\": \"Torneo Tipo Invalido\", \"cupoMaximo\": \"hola\" }")]
        [InlineData("null")]
        public async Task Fuzzing_JsonRotoOTiposIncompatibles_Retorna400ConFormatoErrorUnificado(string rawJson)
        {
            var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
            var res = await _client.PostAsync("/api/torneos", content);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.Contains("\"error\"", body, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("/api/torneos/no-es-un-guid")]
        [InlineData("/api/equipos/guid-malformado-123")]
        [InlineData("/api/jugadores/xyz-999/estadisticas")]
        [InlineData("/api/veedores/id-invalido")]
        public async Task Fuzzing_GuidMalformadoEnRuta_Retorna400BadRequest(string url)
        {
            var res = await _client.GetAsync(url);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.Contains("\"error\"", body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Vector1_EnumsFueraDeRango_Retorna4xxNunca500()
        {
            // 1. Modalidad = 16 -> 400 Bad Request
            var torneoModalidadInvalida = new TorneoCreateDTO
            {
                Nombre = "Torneo Modalidad 16",
                Disciplina = Disciplina.Futbol,
                Modalidad = (Modalidad)16,
                CupoMaximo = 8
            };
            var resMod = await _client.PostAsJsonAsync("/api/torneos", torneoModalidadInvalida);
            Assert.Equal(HttpStatusCode.BadRequest, resMod.StatusCode);

            // 2. Disciplina = -5 -> 404 NotFound (DisciplinaNotFoundException) o 400 BadRequest
            var torneoDisciplinaInvalida = new TorneoCreateDTO
            {
                Nombre = "Torneo Disciplina -5",
                Disciplina = (Disciplina)(-5),
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };
            var resDisc = await _client.PostAsJsonAsync("/api/torneos", torneoDisciplinaInvalida);
            Assert.True(resDisc.StatusCode == HttpStatusCode.NotFound || resDisc.StatusCode == HttpStatusCode.BadRequest);

            // 3. RolUsuario = 99 -> 400 Bad Request
            var usuarioRolInvalido = new UsuarioRegistroDTO
            {
                Nombre = "Pedro",
                Apellido = "Gomez",
                Dni = "40123456",
                Email = "pedro.rol99@test.com",
                Password = "Password123",
                Rol = (RolUsuario)99
            };
            var resRol = await _client.PostAsJsonAsync("/api/usuarios/registro", usuarioRolInvalido);
            Assert.Equal(HttpStatusCode.BadRequest, resRol.StatusCode);

            // 4. TipoIncidencia = 999 -> 400 Bad Request
            var incidenciaInvalida = new IncidenciaCreateDTO
            {
                TipoIncidencia = (TipoIncidencia)999,
                Minuto = 20,
                JugadorId = Guid.NewGuid(),
                EquipoId = Guid.NewGuid()
            };
            var resInc = await _client.PostAsJsonAsync($"/api/partidos/{Guid.NewGuid()}/incidencias", incidenciaInvalida);
            Assert.Equal(HttpStatusCode.BadRequest, resInc.StatusCode);

            // 5. EstadoInscripcion numérico fuera de rango ("99") en CambiarEstado -> 400 Bad Request
            var resEstado = await _client.PutAsJsonAsync($"/api/equipos/{Guid.NewGuid()}/estado", "99");
            Assert.Equal(HttpStatusCode.BadRequest, resEstado.StatusCode);
        }

        [Theory]
        [InlineData("-40123456")]
        [InlineData("40.123.456")]
        [InlineData("4012345A")]
        [InlineData("123")]
        [InlineData("12345678901234567890")]
        [InlineData("        ")]
        public async Task Vector2_AtaquesAlDni_Retorna400BadRequest(string dniMalicioso)
        {
            var regDto = new UsuarioRegistroDTO
            {
                Nombre = "Carlos",
                Apellido = "Seguridad",
                Dni = dniMalicioso,
                Email = $"carlos_{Guid.NewGuid():N}@test.com",
                Password = "Password123",
                Rol = RolUsuario.Capitan
            };

            var resUsuario = await _client.PostAsJsonAsync("/api/usuarios/registro", regDto);
            Assert.Equal(HttpStatusCode.BadRequest, resUsuario.StatusCode);

            var veedorDto = new VeedorCreateDTO
            {
                Nombre = "Roberto",
                Apellido = "Veedor",
                Dni = dniMalicioso,
                Email = $"veedor_{Guid.NewGuid():N}@test.com"
            };

            var resVeedor = await _client.PostAsJsonAsync("/api/veedores", veedorDto);
            Assert.Equal(HttpStatusCode.BadRequest, resVeedor.StatusCode);
        }

        [Fact]
        public async Task Vector2_StringsEnBlancoPayloadGiganteYXss_Retorna400BadRequest()
        {
            // Espacios en blanco
            var torneoWhitespace = new TorneoCreateDTO
            {
                Nombre = "   \t\n  ",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };
            var resWs = await _client.PostAsJsonAsync("/api/torneos", torneoWhitespace);
            Assert.Equal(HttpStatusCode.BadRequest, resWs.StatusCode);

            // Payload gigante (10.000 caracteres)
            var torneoGigante = new TorneoCreateDTO
            {
                Nombre = new string('A', 10000),
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };
            var resGigante = await _client.PostAsJsonAsync("/api/torneos", torneoGigante);
            Assert.Equal(HttpStatusCode.BadRequest, resGigante.StatusCode);

            // Etiquetas <script> XSS
            var torneoXss = new TorneoCreateDTO
            {
                Nombre = "<script>alert('xss')</script>",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            };
            var resXss = await _client.PostAsJsonAsync("/api/torneos", torneoXss);
            Assert.Equal(HttpStatusCode.BadRequest, resXss.StatusCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-16)]
        [InlineData(1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public async Task Vector3_AtaquesNumericosEnCupoMaximo_Retorna400BadRequest(int cupoAbsurdo)
        {
            var dto = new TorneoCreateDTO
            {
                Nombre = $"Torneo Cupo {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = cupoAbsurdo,
                EsPago = false,
                CostoInscripcion = 0
            };

            var res = await _client.PostAsJsonAsync("/api/torneos", dto);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task Vector3_AtaquesNumericosEnCostosMarcadoresYMinutos_Retorna400BadRequest()
        {
            // Costo negativo -0.01 y decimal.MaxValue
            foreach (var costoInvalido in new[] { -0.01m, -5000m, decimal.MaxValue })
            {
                var dto = new TorneoCreateDTO
                {
                    Nombre = $"Torneo Costo {Guid.NewGuid():N}",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8,
                    EsPago = true,
                    CostoInscripcion = costoInvalido
                };
                var res = await _client.PostAsJsonAsync("/api/torneos", dto);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            }

            // Marcadores fuera de rango (-99, int.MaxValue)
            foreach (var marcadorInvalido in new[] { -99, int.MaxValue, 1000 })
            {
                var res = await _client.PutAsJsonAsync($"/api/partidos/{Guid.NewGuid()}/resultado", new CargarResultadoDTO
                {
                    MarcadorLocal = marcadorInvalido,
                    MarcadorVisitante = 1
                });
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            }

            // Minutos de incidencia fuera de rango (0, 5000)
            foreach (var minutoInvalido in new[] { 0, -5, 5000 })
            {
                var res = await _client.PostAsJsonAsync($"/api/partidos/{Guid.NewGuid()}/incidencias", new IncidenciaCreateDTO
                {
                    TipoIncidencia = TipoIncidencia.Gol,
                    Minuto = minutoInvalido,
                    JugadorId = Guid.NewGuid(),
                    EquipoId = Guid.NewGuid()
                });
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            }
        }

        [Fact]
        public async Task Vector4_GuidEmptyYColeccionesNulasOConElementosNull_Retorna400BadRequest()
        {
            // Guid.Empty en rutas
            var resTorneoEmpty = await _client.GetAsync($"/api/torneos/{Guid.Empty}");
            Assert.Equal(HttpStatusCode.BadRequest, resTorneoEmpty.StatusCode);

            var resEquipoEmpty = await _client.GetAsync($"/api/equipos/{Guid.Empty}/estadisticas");
            Assert.Equal(HttpStatusCode.BadRequest, resEquipoEmpty.StatusCode);

            var resPartidoEmpty = await _client.PutAsJsonAsync($"/api/partidos/{Guid.Empty}/resultado", new CargarResultadoDTO { MarcadorLocal = 1, MarcadorVisitante = 0 });
            Assert.Equal(HttpStatusCode.BadRequest, resPartidoEmpty.StatusCode);

            // Lista de jugadores null o con elemento [null]
            var torneoId = Guid.NewGuid();
            var contentNullList = new StringContent(
                $"{{\"torneoId\":\"{torneoId}\",\"nombre\":\"Equipo Null\",\"jugadores\":null}}",
                Encoding.UTF8,
                "application/json");
            var resNullList = await _client.PostAsync($"/api/torneos/{torneoId}/equipos", contentNullList);
            Assert.Equal(HttpStatusCode.BadRequest, resNullList.StatusCode);

            var contentNullElement = new StringContent(
                $"{{\"torneoId\":\"{torneoId}\",\"nombre\":\"Equipo Elem Null\",\"jugadores\":[null]}}",
                Encoding.UTF8,
                "application/json");
            var resNullElement = await _client.PostAsync($"/api/torneos/{torneoId}/equipos", contentNullElement);
            Assert.Equal(HttpStatusCode.BadRequest, resNullElement.StatusCode);
        }

        [Fact]
        public async Task Vector5_FechasYHorariosAbsurdosEnFixture_Retorna400BadRequest()
        {
            var torneoId = Guid.NewGuid();

            // 1. FechaInicio = default(DateTime) (0001-01-01)
            var dtoFechaDefault = new GenerarFixtureRequestDTO
            {
                FechaInicio = default(DateTime),
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "Cancha 1" }
            };
            var resFecha = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/generar-fixture", dtoFechaDefault);
            Assert.Equal(HttpStatusCode.BadRequest, resFecha.StatusCode);

            // 2. IntervaloMinutos = 0 o negativo (-30)
            var dtoIntervaloInvalido = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(5),
                IntervaloMinutos = -30,
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "Cancha 1" }
            };
            var resIntervalo = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/generar-fixture", dtoIntervaloInvalido);
            Assert.Equal(HttpStatusCode.BadRequest, resIntervalo.StatusCode);

            // 3. Franja horaria con fin <= inicio ("18:00-14:00") o formato absurdo ("10000000")
            var dtoHorarioInvertido = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(5),
                Horarios = new List<string> { "18:00-14:00" },
                Sedes = new List<string> { "Cancha 1" }
            };
            var resHorario = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/generar-fixture", dtoHorarioInvertido);
            Assert.Equal(HttpStatusCode.BadRequest, resHorario.StatusCode);

            // 4. Canchas vacías ["", "   "] o duplicadas ["Cancha 1", "Cancha 1"]
            var dtoCanchasVacias = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(5),
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "", "   " }
            };
            var resCanchasVacias = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/generar-fixture", dtoCanchasVacias);
            Assert.Equal(HttpStatusCode.BadRequest, resCanchasVacias.StatusCode);

            var dtoCanchasDuplicadas = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(5),
                Horarios = new List<string> { "14:00" },
                Sedes = new List<string> { "Cancha 1", "cancha 1" }
            };
            var resCanchasDup = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/generar-fixture", dtoCanchasDuplicadas);
            Assert.Equal(HttpStatusCode.BadRequest, resCanchasDup.StatusCode);
        }

        [Fact]
        public async Task Fuzz_Torneo_EsPagoFalseConCostoPositivo_Retorna400BadRequest()
        {
            var payload = new StringContent(
                "{ \"nombre\": \"Torneo Gratis Con Costo\", \"disciplina\": 0, \"modalidad\": 0, \"cupoMaximo\": 8, \"esPago\": false, \"costoInscripcion\": 500 }",
                Encoding.UTF8,
                "application/json");
            var res = await _client.PostAsync("/api/torneos", payload);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.Contains("No se puede ingresar un costo de inscripción si el torneo es gratuito (EsPago = false)", body);
        }

        [Fact]
        public async Task Fuzz_Torneo_DisciplinaInexistenteConDisciplinaIdValido_Retorna404NotFound()
        {
            var payload = new StringContent(
                "{ \"nombre\": \"Torneo Bruno Fuzz\", \"disciplina\": 99, \"disciplinaId\": 1, \"modalidad\": 0, \"cupoMaximo\": 8, \"esPago\": false, \"costoInscripcion\": 0 }",
                Encoding.UTF8,
                "application/json");
            var res = await _client.PostAsync("/api/torneos", payload);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Theory]
        [InlineData("hola123")]
        [InlineData("@@@")]
        [InlineData("12")]
        public async Task Fuzz_TelefonoConLetrasOSimbolos_Retorna400BadRequest(string telefonoInvalido)
        {
            var resUsuario = await _client.PostAsJsonAsync("/api/usuarios/registro", new UsuarioRegistroDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                Email = "juan@test.com",
                Password = "Password123!",
                Telefono = telefonoInvalido,
                Rol = RolUsuario.Capitan
            });
            Assert.Equal(HttpStatusCode.BadRequest, resUsuario.StatusCode);

            var resVeedor = await _client.PostAsJsonAsync("/api/veedores", new VeedorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                Email = "veedor@test.com",
                Telefono = telefonoInvalido
            });
            Assert.Equal(HttpStatusCode.BadRequest, resVeedor.StatusCode);
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("@@@")]
        [InlineData("A")]
        public async Task Fuzz_NombreYApellidoConNumerosOSimbolos_Retorna400BadRequest(string nombreInvalido)
        {
            var resUsuario = await _client.PostAsJsonAsync("/api/usuarios/registro", new UsuarioRegistroDTO
            {
                Nombre = nombreInvalido,
                Apellido = "Perez",
                Dni = "40123456",
                Email = "juan@test.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            });
            Assert.Equal(HttpStatusCode.BadRequest, resUsuario.StatusCode);

            var equipoId = Guid.NewGuid();
            var resJugador = await _client.PostAsJsonAsync($"/api/equipos/{equipoId}/jugadores", new JugadorCreateDTO
            {
                Nombre = "Juan",
                Apellido = nombreInvalido,
                Dni = "40123456",
                EquipoId = equipoId
            });
            Assert.Equal(HttpStatusCode.BadRequest, resJugador.StatusCode);
        }

        [Fact]
        public async Task CasoA_Integration_CrearTorneo_EsPagoFalseYCosto1000_Retorna400BadRequest()
        {
            var res = await _client.PostAsJsonAsync("/api/torneos", new TorneoCreateDTO
            {
                Nombre = "Torneo Gratis Costo 1000",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 1000m
            });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CasoB_Integration_CrearTorneo_Disciplina99YModalidad16_FallaCon400O404()
        {
            var resDisc = await _client.PostAsJsonAsync("/api/torneos", new TorneoCreateDTO
            {
                Nombre = "Torneo Disc 99",
                Disciplina = (Disciplina)99,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8
            });
            Assert.True(resDisc.StatusCode == HttpStatusCode.BadRequest || resDisc.StatusCode == HttpStatusCode.NotFound);

            var resMod = await _client.PostAsJsonAsync("/api/torneos", new TorneoCreateDTO
            {
                Nombre = "Torneo Mod 16",
                Disciplina = Disciplina.Futbol,
                Modalidad = (Modalidad)16,
                CupoMaximo = 8
            });
            Assert.Equal(HttpStatusCode.BadRequest, resMod.StatusCode);
        }

        [Fact]
        public async Task CasoC_Integration_RegistrarJugadorYUsuario_TelefonoTextoInvalidoAbc_Retorna400BadRequest()
        {
            var resUsuario = await _client.PostAsJsonAsync("/api/usuarios/registro", new UsuarioRegistroDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                Email = "juan.abc@test.com",
                Password = "Password123!",
                Telefono = " texto_invalido_abc ",
                Rol = RolUsuario.Capitan
            });
            Assert.Equal(HttpStatusCode.BadRequest, resUsuario.StatusCode);

            var equipoId = Guid.NewGuid();
            var resJugador = await _client.PostAsJsonAsync($"/api/equipos/{equipoId}/jugadores", new JugadorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "40123456",
                Telefono = " texto_invalido_abc ",
                EquipoId = equipoId
            });
            Assert.Equal(HttpStatusCode.BadRequest, resJugador.StatusCode);
        }

        [Fact]
        public async Task CasoD_Integration_RegistrarJugadorYUsuario_NombreConNumerosODniFueraDeRango_Retorna400BadRequest()
        {
            // Nombre con números
            var resUsuarioNombre = await _client.PostAsJsonAsync("/api/usuarios/registro", new UsuarioRegistroDTO
            {
                Nombre = "Juan123",
                Apellido = "Perez",
                Dni = "40123456",
                Email = "juan123@test.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            });
            Assert.Equal(HttpStatusCode.BadRequest, resUsuarioNombre.StatusCode);

            // DNI fuera de rango
            var equipoId = Guid.NewGuid();
            var resJugadorDni = await _client.PostAsJsonAsync($"/api/equipos/{equipoId}/jugadores", new JugadorCreateDTO
            {
                Nombre = "Juan",
                Apellido = "Perez",
                Dni = "123",
                EquipoId = equipoId
            });
            Assert.Equal(HttpStatusCode.BadRequest, resJugadorDni.StatusCode);
        }

        #endregion
    }
}
