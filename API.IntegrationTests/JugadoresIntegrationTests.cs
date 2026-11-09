using System;
using System.Collections.Generic;
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
    public class JugadoresIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public JugadoresIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetEstadisticas_Returns200OK()
        {
            // Arrange (CU-14 Flujo Principal HTTP)
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Integracion Estadisticas {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                Estado = EstadoTorneo.EnDesarrollo
            };
            db.Torneos.Add(torneo);

            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo Integracion {Guid.NewGuid():N}",
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                TorneoId = torneo.Id
            };
            db.Equipos.Add(equipo);

            var jugador = new Jugador
            {
                Id = Guid.NewGuid(),
                Nombre = "Mauro",
                Apellido = "Zarate",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                EquipoId = equipo.Id
            };
            db.Jugadores.Add(jugador);

            var partido = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneo.Id,
                EquipoLocalId = equipo.Id,
                EquipoVisitanteId = Guid.NewGuid(),
                MarcadorLocal = 2,
                MarcadorVisitante = 1,
                Estado = EstadoPartido.Finalizado,
                FechaHora = DateTime.UtcNow,
                Sede = "Cancha Central",
                Ronda = 1
            };
            db.Partidos.Add(partido);

            var gol = new Incidencia
            {
                Id = Guid.NewGuid(),
                PartidoId = partido.Id,
                EquipoId = equipo.Id,
                JugadorId = jugador.Id,
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = 15
            };
            var tarjeta = new Incidencia
            {
                Id = Guid.NewGuid(),
                PartidoId = partido.Id,
                EquipoId = equipo.Id,
                JugadorId = jugador.Id,
                TipoIncidencia = TipoIncidencia.TarjetaAmarilla,
                Minuto = 40
            };
            db.Incidencias.AddRange(gol, tarjeta);

            await db.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/jugadores/{jugador.Id}/estadisticas");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var stats = await response.Content.ReadFromJsonAsync<JugadorEstadisticasDTO>();
            Assert.NotNull(stats);
            Assert.Equal(jugador.Id, stats.JugadorId);
            Assert.Equal(1, stats.Goles);
            Assert.Equal(1, stats.TarjetasAmarillas);
            Assert.Equal(equipo.Nombre, stats.EquipoActual);
            Assert.Contains(torneo.Nombre, stats.HistorialTorneos);
        }

        [Fact]
        public async Task GetEstadisticas_WhenEntityDoesNotExist_Returns404NotFound()
        {
            // Act (CU-14 Flujo Alternativo 3a HTTP)
            var response = await _client.GetAsync($"/api/jugadores/{Guid.NewGuid()}/estadisticas");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        #region RN-03 / CU-05: Control de EdicionListaHabilitada en API

        [Fact]
        public async Task CreateJugador_EquipoEdicionDeshabilitada_Retorna409Conflict()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Edicion {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                };
                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = $"Equipo Bloqueado {Guid.NewGuid():N}".Substring(0, 20),
                    TorneoId = torneoId,
                    EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                    EdicionListaHabilitada = false
                };
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                await db.SaveChangesAsync();
            }

            var dto = new JugadorCreateDTO
            {
                Nombre = "Enzo",
                Apellido = "Fernandez",
                Dni = "34555666",
                EquipoId = equipoId
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/equipos/{equipoId}/jugadores", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // 409 Conflict
        }

        [Fact]
        public async Task CreateJugador_EquipoConfirmado_Retorna409Conflict()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Confirmado {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                };
                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = $"Equipo Confirmado {Guid.NewGuid():N}".Substring(0, 20),
                    TorneoId = torneoId,
                    EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                    EdicionListaHabilitada = false
                };
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                await db.SaveChangesAsync();
            }

            var dto = new JugadorCreateDTO
            {
                Nombre = "Cristian",
                Apellido = "Romero",
                Dni = "35555666",
                EquipoId = equipoId
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/equipos/{equipoId}/jugadores", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // 409 Conflict
        }

        [Fact]
        public async Task DeleteJugador_EquipoEdicionDeshabilitada_Retorna409Conflict()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Delete {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                };
                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = $"Equipo Con Jugador {Guid.NewGuid():N}".Substring(0, 20),
                    TorneoId = torneoId,
                    EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                    EdicionListaHabilitada = false
                };
                var jugador = new Jugador
                {
                    Id = jugadorId,
                    Nombre = "Nahuel",
                    Apellido = "Molina",
                    Dni = "36555666",
                    EquipoId = equipoId
                };
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                db.Jugadores.Add(jugador);
                await db.SaveChangesAsync();
            }

            // Act
            var response = await _client.DeleteAsync($"/api/jugadores/{jugadorId}");

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // 409 Conflict
        }

        [Fact]
        public async Task CreateYDeleteJugador_EquipoRechazadoObservadoConEdicionHabilitada_OperacionExitosa()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Observado {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                };
                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = $"Equipo Para Corregir {Guid.NewGuid():N}".Substring(0, 20),
                    TorneoId = torneoId,
                    EstadoInscripcion = EstadoInscripcion.RechazadoObservado,
                    EdicionListaHabilitada = true,
                    MotivoRechazo = "Documentación ilegible"
                };
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                await db.SaveChangesAsync();
            }

            var dto = new JugadorCreateDTO
            {
                Nombre = "Marcos",
                Apellido = "Acuna",
                Dni = "37555666",
                EquipoId = equipoId
            };

            // Act Create: Debe permitir agregar jugador
            var createRes = await _client.PostAsJsonAsync($"/api/equipos/{equipoId}/jugadores", dto);
            Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);

            var createdJugador = await createRes.Content.ReadFromJsonAsync<JugadorResponseDTO>();
            Assert.NotNull(createdJugador);
            Assert.Equal("37555666", createdJugador.Dni);

            // Act Delete: Debe permitir eliminar jugador en estado RechazadoObservado
            var deleteRes = await _client.DeleteAsync($"/api/jugadores/{createdJugador.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
        }

        #endregion

        #region Mitigación IDOR y Titularidad de Equipos/Jugadores (CU-05)

        [Fact]
        public async Task CU05_AntiIDOR_CapitanAjeno_IntentaEliminarJugador_Retorna403Forbidden()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();
            var capitanDuenio = Guid.NewGuid();
            var capitanAtacante = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo IDOR {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                };
                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = $"Equipo Victima {Guid.NewGuid():N}".Substring(0, 20),
                    TorneoId = torneoId,
                    CapitanId = capitanDuenio,
                    EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                    EdicionListaHabilitada = true
                };
                var jugador = new Jugador
                {
                    Id = jugadorId,
                    Nombre = "Lionel",
                    Apellido = "Messi",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    EquipoId = equipoId
                };
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                db.Jugadores.Add(jugador);
                await db.SaveChangesAsync();
            }

            // Act: Capitán atacante intenta eliminar al jugador de la víctima (Ataque IDOR)
            var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/jugadores/{jugadorId}");
            req.Headers.Add("X-Test-Role", "Capitan");
            req.Headers.Add("X-Test-UserId", capitanAtacante.ToString());

            var response = await _client.SendAsync(req);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // 403 Forbidden

            // Verificar que el jugador sigue existiendo en base de datos
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var jugadorEnDb = await db.Jugadores.FindAsync(jugadorId);
                Assert.NotNull(jugadorEnDb);
            }
        }

        [Fact]
        public async Task CU05_AntiIDOR_CapitanAjeno_IntentaAgregarJugador_Retorna403Forbidden()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var capitanDuenio = Guid.NewGuid();
            var capitanAtacante = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo IDOR Add {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                };
                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = $"Equipo Victima Add {Guid.NewGuid():N}".Substring(0, 20),
                    TorneoId = torneoId,
                    CapitanId = capitanDuenio,
                    EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                    EdicionListaHabilitada = true
                };
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                await db.SaveChangesAsync();
            }

            var dto = new JugadorCreateDTO
            {
                Nombre = "Infiltrado",
                Apellido = "Malicioso",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                EquipoId = equipoId
            };

            // Act: Capitán atacante intenta registrar un jugador en el equipo ajeno
            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/equipos/{equipoId}/jugadores")
            {
                Content = JsonContent.Create(dto)
            };
            req.Headers.Add("X-Test-Role", "Capitan");
            req.Headers.Add("X-Test-UserId", capitanAtacante.ToString());

            var response = await _client.SendAsync(req);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // 403 Forbidden
        }

        [Fact]
        public async Task CU05_CapitanDuenio_OperacionesLegitimas_CrearYEliminarJugador_Exitoso()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var capitanDuenio = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Duenio {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                };
                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = $"Equipo Propio {Guid.NewGuid():N}".Substring(0, 20),
                    TorneoId = torneoId,
                    CapitanId = capitanDuenio,
                    EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                    EdicionListaHabilitada = true
                };
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                await db.SaveChangesAsync();
            }

            var dto = new JugadorCreateDTO
            {
                Nombre = "Legitimo",
                Apellido = "Jugador",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                EquipoId = equipoId
            };

            // Act 1: El capitán propietario crea un jugador en su propio equipo
            var reqCreate = new HttpRequestMessage(HttpMethod.Post, $"/api/equipos/{equipoId}/jugadores")
            {
                Content = JsonContent.Create(dto)
            };
            reqCreate.Headers.Add("X-Test-Role", "Capitan");
            reqCreate.Headers.Add("X-Test-UserId", capitanDuenio.ToString());

            var resCreate = await _client.SendAsync(reqCreate);
            Assert.Equal(HttpStatusCode.OK, resCreate.StatusCode);

            var createdJugador = await resCreate.Content.ReadFromJsonAsync<JugadorResponseDTO>();
            Assert.NotNull(createdJugador);

            // Act 2: El capitán propietario elimina al jugador de su propio equipo
            var reqDelete = new HttpRequestMessage(HttpMethod.Delete, $"/api/jugadores/{createdJugador.Id}");
            reqDelete.Headers.Add("X-Test-Role", "Capitan");
            reqDelete.Headers.Add("X-Test-UserId", capitanDuenio.ToString());

            var resDelete = await _client.SendAsync(reqDelete);
            Assert.Equal(HttpStatusCode.NoContent, resDelete.StatusCode);
        }

        [Fact]
        public async Task CU05_Organizador_EliminarJugadorDeCualquierEquipo_Exitoso()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var equipoId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();
            var capitanDuenio = Guid.NewGuid();
            var organizadorId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Org Delete {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                };
                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = $"Equipo Capitaneado {Guid.NewGuid():N}".Substring(0, 20),
                    TorneoId = torneoId,
                    CapitanId = capitanDuenio,
                    EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                    EdicionListaHabilitada = true
                };
                var jugador = new Jugador
                {
                    Id = jugadorId,
                    Nombre = "Lautaro",
                    Apellido = "Martinez",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    EquipoId = equipoId
                };
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                db.Jugadores.Add(jugador);
                await db.SaveChangesAsync();
            }

            // Act: Organizador elimina jugador de equipo ajeno
            var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/jugadores/{jugadorId}");
            req.Headers.Add("X-Test-Role", "Organizador");
            req.Headers.Add("X-Test-UserId", organizadorId.ToString());

            var response = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task CU05_CrearEquipo_AsociaCapitanIdDesdeClaimsJWT()
        {
            // Arrange
            var torneoId = Guid.NewGuid();
            var capitanNuevo = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Alta Equipo {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8,
                    Estado = EstadoTorneo.Abierto
                };
                db.Torneos.Add(torneo);
                await db.SaveChangesAsync();
            }

            var dto = new EquipoCreateDTO
            {
                Nombre = $"Equipo Nuevo {Guid.NewGuid():N}".Substring(0, 20),
                TorneoId = torneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Jugador", Apellido = "Uno", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Jugador", Apellido = "Dos", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Jugador", Apellido = "Tres", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Jugador", Apellido = "Cuatro", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Jugador", Apellido = "Cinco", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Jugador", Apellido = "Seis", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Jugador", Apellido = "Siete", Dni = $"{Random.Shared.Next(10000000, 99999999)}" }
                }
            };

            // Act: Capitán crea equipo
            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/torneos/{torneoId}/equipos")
            {
                Content = JsonContent.Create(dto)
            };
            req.Headers.Add("X-Test-Role", "Capitan");
            req.Headers.Add("X-Test-UserId", capitanNuevo.ToString());

            var response = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var created = await response.Content.ReadFromJsonAsync<EquipoResponseDTO>();
            Assert.NotNull(created);
            Assert.Equal(capitanNuevo, created.CapitanId);

            // Verificar persistencia en base de datos
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var equipoEnDb = await db.Equipos.FindAsync(created.Id);
                Assert.NotNull(equipoEnDb);
                Assert.Equal(capitanNuevo, equipoEnDb.CapitanId);
            }
        }

        #endregion
    }
}
