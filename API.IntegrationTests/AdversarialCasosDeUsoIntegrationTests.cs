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
                    new() { Nombre = "A", Apellido = "B", Dni = "11111111" }
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
                    new() { Nombre = "Jugador 1", Apellido = "Uno", Dni = "40111222" },
                    new() { Nombre = "Jugador 2", Apellido = "Dos", Dni = "40111223" }
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
                    new() { Nombre = "J1", Apellido = "A", Dni = "40999999" },
                    new() { Nombre = "J2", Apellido = "B", Dni = "40999999" }, // DUPLICADO INTERNO
                    new() { Nombre = "J3", Apellido = "C", Dni = "40999991" },
                    new() { Nombre = "J4", Apellido = "D", Dni = "40999992" },
                    new() { Nombre = "J5", Apellido = "E", Dni = "40999993" }
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
                    new() { Nombre = "A1", Apellido = "A", Dni = dniCompartido },
                    new() { Nombre = "A2", Apellido = "B", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "A3", Apellido = "C", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "A4", Apellido = "D", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "A5", Apellido = "E", Dni = $"{new Random().Next(10000000, 99999999)}" }
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
                    new() { Nombre = "B1", Apellido = "A", Dni = dniCompartido }, // VIOLA RN-02
                    new() { Nombre = "B2", Apellido = "B", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "B3", Apellido = "C", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "B4", Apellido = "D", Dni = $"{new Random().Next(10000000, 99999999)}" },
                    new() { Nombre = "B5", Apellido = "E", Dni = $"{new Random().Next(10000000, 99999999)}" }
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
    }
}
