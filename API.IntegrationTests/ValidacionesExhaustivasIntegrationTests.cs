using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DataAccess.Data;
using DataAccess.Entities;
using Microsoft.Extensions.DependencyInjection;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    /// <summary>
    /// Batería de pruebas de integración adversarial y fuzzing exhaustivo.
    /// Simula los intentos de inyección de datos inválidos en endpoints de creación y actualización (POST / PUT / PATCH)
    /// garantizando que:
    /// 1. NUNCA devuelvan 200 OK / 201 Created ante una carga mal formada.
    /// 2. JAMÁS devuelvan 500 Internal Server Error (siempre códigos controlados 400, 404, 409 o 422).
    /// </summary>
    public class ValidacionesExhaustivasIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public ValidacionesExhaustivasIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        #region Helper de Aserción Robusta

        private static void AssertErrorControladoNo500Ni2xx(HttpResponseMessage response)
        {
            // Regla 1: NUNCA debe devolver 200 OK / 201 Created (ni ningún 2xx)
            Assert.False(response.IsSuccessStatusCode,
                $"Vulnerabilidad detectada: el endpoint respondió exitosamente ({response.StatusCode}) con datos inválidos.");

            // Regla 2: JAMÁS debe devolver 500 Internal Server Error
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);

            // Regla 3: Siempre debe ser un código de cliente controlado (400, 404, 409 o 422)
            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.NotFound ||
                response.StatusCode == HttpStatusCode.Conflict ||
                response.StatusCode == HttpStatusCode.UnprocessableEntity,
                $"Se esperaba un código de error de cliente controlado (400, 404, 409 o 422), pero se recibió: {response.StatusCode}");
        }

        #endregion

        #region Helper Fixtures de Datos de Prueba

        private async Task<Guid> SeedTorneoAbiertoAsync()
        {
            var torneoId = Guid.NewGuid();
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = $"Torneo Abierto {Guid.NewGuid():N}".Substring(0, 25),
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 16,
                EsPago = false,
                CostoInscripcion = 0,
                Estado = EstadoTorneo.Abierto
            };

            db.Torneos.Add(torneo);
            await db.SaveChangesAsync();
            return torneoId;
        }

        private async Task<(Guid partidoId, Guid localId, Guid visitanteId, Guid jugadorId)> SeedPartidoConJugadorAsync()
        {
            var torneoId = Guid.NewGuid();
            var localId = Guid.NewGuid();
            var visitanteId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();
            var partidoId = Guid.NewGuid();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = torneoId,
                Nombre = $"Torneo Incidencias {Guid.NewGuid():N}".Substring(0, 25),
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                CostoInscripcion = 0,
                Estado = EstadoTorneo.EnDesarrollo
            };

            var local = new Equipo
            {
                Id = localId,
                Nombre = $"Local {Guid.NewGuid():N}".Substring(0, 15),
                TorneoId = torneoId,
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
            };

            var visitante = new Equipo
            {
                Id = visitanteId,
                Nombre = $"Visitante {Guid.NewGuid():N}".Substring(0, 15),
                TorneoId = torneoId,
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
            };

            var jugador = new Jugador
            {
                Id = jugadorId,
                Nombre = "Lionel",
                Apellido = "Messi",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                EquipoId = localId,
                Inhabilitado = false
            };

            var partido = new Partido
            {
                Id = partidoId,
                TorneoId = torneoId,
                EquipoLocalId = localId,
                EquipoVisitanteId = visitanteId,
                FechaHora = DateTime.Today.AddHours(16),
                Sede = "Estadio Central",
                Ronda = 1,
                Estado = EstadoPartido.EnJuego,
                MarcadorLocal = 0,
                MarcadorVisitante = 0
            };

            db.Torneos.Add(torneo);
            db.Equipos.AddRange(local, visitante);
            db.Jugadores.Add(jugador);
            db.Partidos.Add(partido);
            await db.SaveChangesAsync();

            return (partidoId, localId, visitanteId, jugadorId);
        }

        #endregion

        #region 1. Módulo Torneos (POST /api/torneos y PUT /api/torneos/{id})

        [Theory]
        [InlineData(-1)]
        [InlineData(99)]
        [InlineData(500)]
        public async Task Torneo_Crear_DisciplinaFueraDeEnum_Retorna4xxNunca500(int disciplinaInvalida)
        {
            var payload = new
            {
                Nombre = $"Torneo {Guid.NewGuid():N}".Substring(0, 25),
                Disciplina = disciplinaInvalida,
                Modalidad = (int)Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0m
            };

            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(2)]
        [InlineData(16)]
        public async Task Torneo_Crear_ModalidadFueraDeEnum_Retorna4xxNunca500(int modalidadInvalida)
        {
            var payload = new
            {
                Nombre = $"Torneo {Guid.NewGuid():N}".Substring(0, 25),
                Disciplina = (int)Disciplina.Futbol,
                Modalidad = modalidadInvalida,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0m
            };

            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(-10)]
        public async Task Torneo_Crear_EsPagoFalse_ConCostoInvalido_Retorna4xxNunca500(decimal costoInvalido)
        {
            var payload = new TorneoCreateDTO
            {
                Nombre = $"Torneo Gratis {Guid.NewGuid():N}".Substring(0, 25),
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = costoInvalido
            };

            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-500)]
        public async Task Torneo_Crear_EsPagoTrue_ConCostoInvalido_Retorna4xxNunca500(decimal costoInvalido)
        {
            var payload = new TorneoCreateDTO
            {
                Nombre = $"Torneo Pago {Guid.NewGuid():N}".Substring(0, 25),
                Disciplina = Disciplina.ESports,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = costoInvalido
            };

            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-4)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(999999)]
        public async Task Torneo_Crear_CupoMaximoInvalido_Retorna4xxNunca500(int cupoInvalido)
        {
            var payload = new TorneoCreateDTO
            {
                Nombre = $"Torneo Cupo {Guid.NewGuid():N}".Substring(0, 25),
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = cupoInvalido,
                EsPago = false,
                CostoInscripcion = 0m
            };

            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("A")]
        [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // 151 chars
        public async Task Torneo_Crear_NombreInvalido_Retorna4xxNunca500(string nombreInvalido)
        {
            var payload = new TorneoCreateDTO
            {
                Nombre = nombreInvalido,
                Disciplina = Disciplina.ESports,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0m
            };

            var response = await _client.PostAsJsonAsync("/api/torneos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(1)]
        [InlineData(500)]
        public async Task Torneo_Actualizar_CupoMaximoInvalido_Retorna4xxNunca500(int cupoInvalido)
        {
            var torneoId = await SeedTorneoAbiertoAsync();
            var payload = new TorneoUpdateDTO
            {
                Nombre = "Torneo Actualizado Valido",
                CupoMaximo = cupoInvalido,
                CostoInscripcion = 0m
            };

            var response = await _client.PutAsJsonAsync($"/api/torneos/{torneoId}", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("Descripción con <style>body{display:none;}</style>")]
        public async Task Torneo_Actualizar_DescripcionConHtml_Retorna4xxNunca500(string descripcionInvalida)
        {
            var torneoId = await SeedTorneoAbiertoAsync();
            var payload = new TorneoUpdateDTO
            {
                Nombre = "Torneo Actualizado Valido",
                Descripcion = descripcionInvalida,
                CupoMaximo = 8,
                CostoInscripcion = 0m
            };

            var response = await _client.PutAsJsonAsync($"/api/torneos/{torneoId}", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        #endregion

        #region 2. Módulo Equipos y Jugadores (POST /api/torneos/{id}/equipos y POST /api/equipos/{id}/jugadores)

        [Theory]
        [InlineData("abc")]
        [InlineData("123")]
        [InlineData("@@@-1234")]
        [InlineData("12345678901234567890")]
        public async Task Equipo_JugadorTelefonoInvalido_Retorna4xxNunca500(string telefonoInvalido)
        {
            var torneoId = await SeedTorneoAbiertoAsync();
            var payload = new EquipoCreateDTO
            {
                Nombre = $"Equipo {Guid.NewGuid():N}".Substring(0, 20),
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new JugadorCreateDTO
                    {
                        Nombre = "Lionel",
                        Apellido = "Messi",
                        Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                        Telefono = telefonoInvalido
                    }
                }
            };

            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/equipos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("123")]
        [InlineData("123456789")]
        [InlineData("4012345A")]
        [InlineData("-40123456")]
        [InlineData("40.123.456")]
        public async Task Equipo_JugadorDniInvalido_Retorna4xxNunca500(string dniInvalido)
        {
            var torneoId = await SeedTorneoAbiertoAsync();
            var payload = new EquipoCreateDTO
            {
                Nombre = $"Equipo {Guid.NewGuid():N}".Substring(0, 20),
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new JugadorCreateDTO
                    {
                        Nombre = "Lionel",
                        Apellido = "Messi",
                        Dni = dniInvalido,
                        Telefono = "1122334455"
                    }
                }
            };

            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/equipos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("Juan123")]
        [InlineData("@@@")]
        public async Task Equipo_JugadorNombreInvalido_Retorna4xxNunca500(string nombreInvalido)
        {
            var torneoId = await SeedTorneoAbiertoAsync();
            var payload = new EquipoCreateDTO
            {
                Nombre = $"Equipo {Guid.NewGuid():N}".Substring(0, 20),
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new JugadorCreateDTO
                    {
                        Nombre = nombreInvalido,
                        Apellido = "Perez",
                        Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                        Telefono = "1122334455"
                    }
                }
            };

            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/equipos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("Perez123")]
        [InlineData("@@@")]
        public async Task Equipo_JugadorApellidoInvalido_Retorna4xxNunca500(string apellidoInvalido)
        {
            var torneoId = await SeedTorneoAbiertoAsync();
            var payload = new EquipoCreateDTO
            {
                Nombre = $"Equipo {Guid.NewGuid():N}".Substring(0, 20),
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new JugadorCreateDTO
                    {
                        Nombre = "Carlos",
                        Apellido = apellidoInvalido,
                        Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                        Telefono = "1122334455"
                    }
                }
            };

            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/equipos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("12")]
        [InlineData("123456")]
        [InlineData("@@@###")]
        public async Task Equipo_NombreInvalido_Retorna4xxNunca500(string nombreEquipoInvalido)
        {
            var torneoId = await SeedTorneoAbiertoAsync();
            var payload = new EquipoCreateDTO
            {
                Nombre = nombreEquipoInvalido,
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new JugadorCreateDTO
                    {
                        Nombre = "Carlos",
                        Apellido = "Tevez",
                        Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                        Telefono = "1122334455"
                    }
                }
            };

            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/equipos", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("Juan123", "Perez", "33111222", "1122334455")]
        [InlineData("Juan", "Perez99", "33111222", "1122334455")]
        [InlineData("Juan", "Perez", "DniInvalido", "1122334455")]
        [InlineData("Juan", "Perez", "33111222", "TelefonoInvalido")]
        public async Task JugadorIndividual_Crear_DatosInvalidos_Retorna4xxNunca500(string nombre, string apellido, string dni, string telefono)
        {
            var (_, localId, _, _) = await SeedPartidoConJugadorAsync();

            var payload = new JugadorCreateDTO
            {
                Nombre = nombre,
                Apellido = apellido,
                Dni = dni,
                Telefono = telefono,
                EquipoId = localId
            };

            var response = await _client.PostAsJsonAsync($"/api/equipos/{localId}/jugadores", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        #endregion

        #region 3. Módulo Usuarios y Veedores (POST /api/usuarios/registro y POST /api/veedores)

        [Theory]
        [InlineData("123")]
        [InlineData("123456789")]
        [InlineData("4012345A")]
        [InlineData("-40123456")]
        [InlineData("40.123.456")]
        public async Task Usuario_Registro_DniInvalido_Retorna4xxNunca500(string dniInvalido)
        {
            var payload = new UsuarioRegistroDTO
            {
                Nombre = "Facundo",
                Apellido = "Quiroga",
                Dni = dniInvalido,
                Email = $"facundo_{Guid.NewGuid():N}@test.com",
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("123")]
        [InlineData("@@@-1234")]
        [InlineData("12345678901234567890")]
        public async Task Usuario_Registro_TelefonoInvalido_Retorna4xxNunca500(string telefonoInvalido)
        {
            var payload = new UsuarioRegistroDTO
            {
                Nombre = "Facundo",
                Apellido = "Quiroga",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = $"facundo_{Guid.NewGuid():N}@test.com",
                Telefono = telefonoInvalido,
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("email-sin-arroba")]
        [InlineData("email@")]
        [InlineData("@dominio.com")]
        [InlineData("email@sindominio")]
        public async Task Usuario_Registro_EmailInvalido_Retorna4xxNunca500(string emailInvalido)
        {
            var payload = new UsuarioRegistroDTO
            {
                Nombre = "Facundo",
                Apellido = "Quiroga",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = emailInvalido,
                Password = "Password123!",
                Rol = RolUsuario.Capitan
            };

            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData(99)]
        [InlineData(-1)]
        public async Task Usuario_Registro_RolFueraDeEnum_Retorna4xxNunca500(int rolInvalido)
        {
            var payload = new
            {
                Nombre = "Facundo",
                Apellido = "Quiroga",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = $"facundo_{Guid.NewGuid():N}@test.com",
                Password = "Password123!",
                Rol = rolInvalido
            };

            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("debil")]
        [InlineData("sololetras")]
        [InlineData("12345678")]
        [InlineData("short1")]
        public async Task Usuario_Registro_PasswordDebil_Retorna4xxNunca500(string passwordDebil)
        {
            var payload = new UsuarioRegistroDTO
            {
                Nombre = "Facundo",
                Apellido = "Quiroga",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = $"facundo_{Guid.NewGuid():N}@test.com",
                Password = passwordDebil,
                Rol = RolUsuario.Capitan
            };

            var response = await _client.PostAsJsonAsync("/api/usuarios/registro", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("123")]
        [InlineData("123456789")]
        [InlineData("4012345A")]
        [InlineData("-40123456")]
        [InlineData("40.123.456")]
        public async Task Veedor_Crear_DniInvalido_Retorna4xxNunca500(string dniInvalido)
        {
            var payload = new VeedorCreateDTO
            {
                Nombre = "Veedor",
                Apellido = "Oficial",
                Dni = dniInvalido,
                Email = $"veedor_{Guid.NewGuid():N}@test.com",
                Telefono = "1122334455"
            };

            var response = await _client.PostAsJsonAsync("/api/veedores", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("123")]
        [InlineData("@@@-1234")]
        [InlineData("12345678901234567890")]
        public async Task Veedor_Crear_TelefonoInvalido_Retorna4xxNunca500(string telefonoInvalido)
        {
            var payload = new VeedorCreateDTO
            {
                Nombre = "Veedor",
                Apellido = "Oficial",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = $"veedor_{Guid.NewGuid():N}@test.com",
                Telefono = telefonoInvalido
            };

            var response = await _client.PostAsJsonAsync("/api/veedores", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("email-sin-arroba")]
        [InlineData("email@")]
        [InlineData("@dominio.com")]
        [InlineData("email@sindominio")]
        public async Task Veedor_Crear_EmailInvalido_Retorna4xxNunca500(string emailInvalido)
        {
            var payload = new VeedorCreateDTO
            {
                Nombre = "Veedor",
                Apellido = "Oficial",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = emailInvalido,
                Telefono = "1122334455"
            };

            var response = await _client.PostAsJsonAsync("/api/veedores", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData("Carlos123", "Perez")]
        [InlineData("Carlos", "Perez123")]
        [InlineData("@@@", "Perez")]
        [InlineData("Carlos", "@@@")]
        public async Task Veedor_Crear_NombreOApellidoInvalido_Retorna4xxNunca500(string nombre, string apellido)
        {
            var payload = new VeedorCreateDTO
            {
                Nombre = nombre,
                Apellido = apellido,
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                Email = $"veedor_{Guid.NewGuid():N}@test.com",
                Telefono = "1122334455"
            };

            var response = await _client.PostAsJsonAsync("/api/veedores", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        #endregion

        #region 4. Módulo Partidos e Incidencias (PUT /api/partidos/{id}/resultado y POST /api/partidos/{id}/incidencias)

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        [InlineData(-1, -1)]
        [InlineData(-10, 5)]
        [InlineData(5, -10)]
        public async Task Partido_CargarResultado_GolesNegativos_Retorna4xxNunca500(int golesLocales, int golesVisitantes)
        {
            var (partidoId, _, _, _) = await SeedPartidoConJugadorAsync();

            var payload = new CargarResultadoDTO
            {
                MarcadorLocal = golesLocales,
                MarcadorVisitante = golesVisitantes
            };

            var response = await _client.PutAsJsonAsync($"/api/partidos/{partidoId}/resultado", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(151)]
        [InlineData(999)]
        public async Task Incidencia_Registrar_MinutoInvalido_Retorna4xxNunca500(int minutoInvalido)
        {
            var (partidoId, localId, _, jugadorId) = await SeedPartidoConJugadorAsync();

            var payload = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = minutoInvalido,
                JugadorId = jugadorId,
                EquipoId = localId,
                Observaciones = "Minuto fuera de rango"
            };

            var response = await _client.PostAsJsonAsync($"/api/partidos/{partidoId}/incidencias", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        [Theory]
        [InlineData(88)]
        [InlineData(-1)]
        [InlineData(999)]
        public async Task Incidencia_Registrar_TipoIncidenciaFueraDeEnum_Retorna4xxNunca500(int tipoInvalido)
        {
            var (partidoId, localId, _, jugadorId) = await SeedPartidoConJugadorAsync();

            var payload = new
            {
                TipoIncidencia = tipoInvalido,
                Minuto = 25,
                JugadorId = jugadorId,
                EquipoId = localId,
                Observaciones = "Tipo fuera de enum"
            };

            var response = await _client.PostAsJsonAsync($"/api/partidos/{partidoId}/incidencias", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        #endregion

        #region 5. Módulo Inscripciones y Rechazo (PATCH /api/torneos/{torneoId}/equipos/{equipoId}/rechazar)

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ab")] // Menos de 3 caracteres
        [InlineData("<script>alert('xss')</script>")]
        public async Task Inscripcion_Rechazar_MotivoInvalido_Retorna4xxNunca500(string motivoInvalido)
        {
            var torneoId = await SeedTorneoAbiertoAsync();
            var equipoId = Guid.NewGuid();

            var payload = new RechazarInscripcionDTO
            {
                Motivo = motivoInvalido
            };

            var response = await _client.PatchAsJsonAsync($"/api/torneos/{torneoId}/equipos/{equipoId}/rechazar", payload);

            AssertErrorControladoNo500Ni2xx(response);
        }

        #endregion
    }
}
