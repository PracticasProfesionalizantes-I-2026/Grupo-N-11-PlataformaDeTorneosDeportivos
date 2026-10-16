using System;
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
    public class IncidenciasIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly CustomWebApplicationFactory<API.Program> _factory;
        private readonly HttpClient _client;

        public IncidenciasIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task RegistrarIncidencia_WithValidData_Returns201Created()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();
            var localId = Guid.NewGuid();
            var visitanteId = Guid.NewGuid();
            var jugadorId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var torneo = new Torneo { Id = torneoId, Nombre = "Torneo Incidencias 1", Disciplina = Disciplina.Futbol, Modalidad = Modalidad.Liga, CupoMaximo = 8, CostoInscripcion = 0 };
                var local = new Equipo { Id = localId, Nombre = "Equipo Local A", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
                var visitante = new Equipo { Id = visitanteId, Nombre = "Equipo Visitante B", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
                var jugador = new Jugador { Id = jugadorId, Nombre = "Goleador", Apellido = "Uno", Dni = "11112222", EquipoId = localId, Inhabilitado = false };

                var partido = new Partido
                {
                    Id = partidoId,
                    TorneoId = torneoId,
                    EquipoLocalId = localId,
                    EquipoVisitanteId = visitanteId,
                    FechaHora = DateTime.Today.AddHours(14),
                    Sede = "Estadio 1",
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
            }

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = 15,
                JugadorId = jugadorId,
                EquipoId = localId,
                Observaciones = "Gol de penal"
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/partidos/{partidoId}/incidencias", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<IncidenciaResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(partidoId, result.PartidoId);
            Assert.Equal(1, result.MarcadorLocalParcial);
        }

        [Fact]
        public async Task RegistrarIncidencia_WithNegativeMinute_Returns400BadRequest()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = -5, // Invalido
                JugadorId = Guid.NewGuid(),
                EquipoId = Guid.NewGuid()
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/partidos/{partidoId}/incidencias", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RegistrarIncidencia_WhenPartidoNotFound_Returns404NotFound()
        {
            // Arrange
            var partidoInexistenteId = Guid.NewGuid();
            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = 10,
                JugadorId = Guid.NewGuid(),
                EquipoId = Guid.NewGuid()
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/partidos/{partidoInexistenteId}/incidencias", dto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RegistrarIncidencia_WhenPartidoFinished_Returns409Conflict()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();
            var localId = Guid.NewGuid();
            var visitanteId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo { Id = torneoId, Nombre = "Torneo Incidencias Finalizado", Disciplina = Disciplina.Futbol, Modalidad = Modalidad.Liga, CupoMaximo = 8, CostoInscripcion = 0 };
                var local = new Equipo { Id = localId, Nombre = "Equipo L2", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
                var visitante = new Equipo { Id = visitanteId, Nombre = "Equipo V2", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };

                var partido = new Partido
                {
                    Id = partidoId,
                    TorneoId = torneoId,
                    EquipoLocalId = localId,
                    EquipoVisitanteId = visitanteId,
                    FechaHora = DateTime.Today.AddHours(10),
                    Sede = "Estadio 2",
                    Ronda = 1,
                    Estado = EstadoPartido.Finalizado
                };

                db.Torneos.Add(torneo);
                db.Equipos.AddRange(local, visitante);
                db.Partidos.Add(partido);
                await db.SaveChangesAsync();
            }

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Gol,
                Minuto = 90,
                JugadorId = Guid.NewGuid(),
                EquipoId = localId
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/partidos/{partidoId}/incidencias", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task RegistrarIncidencia_WhenPlayerNotInTeams_Returns422UnprocessableEntity()
        {
            // Arrange
            var partidoId = Guid.NewGuid();
            var torneoId = Guid.NewGuid();
            var localId = Guid.NewGuid();
            var visitanteId = Guid.NewGuid();
            var otroEquipoId = Guid.NewGuid();
            var jugadorAjenoId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo { Id = torneoId, Nombre = "Torneo Incidencias 3", Disciplina = Disciplina.Futbol, Modalidad = Modalidad.Liga, CupoMaximo = 8, CostoInscripcion = 0 };
                var local = new Equipo { Id = localId, Nombre = "Equipo L3", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
                var visitante = new Equipo { Id = visitanteId, Nombre = "Equipo V3", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
                var otroEquipo = new Equipo { Id = otroEquipoId, Nombre = "Equipo Externo", TorneoId = torneoId, EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado };
                var jugadorAjeno = new Jugador { Id = jugadorAjenoId, Nombre = "Infiltrado", Apellido = "X", Dni = "99998888", EquipoId = otroEquipoId, Inhabilitado = false };

                var partido = new Partido
                {
                    Id = partidoId,
                    TorneoId = torneoId,
                    EquipoLocalId = localId,
                    EquipoVisitanteId = visitanteId,
                    FechaHora = DateTime.Today.AddHours(16),
                    Sede = "Estadio 3",
                    Ronda = 1,
                    Estado = EstadoPartido.EnJuego
                };

                db.Torneos.Add(torneo);
                db.Equipos.AddRange(local, visitante, otroEquipo);
                db.Jugadores.Add(jugadorAjeno);
                db.Partidos.Add(partido);
                await db.SaveChangesAsync();
            }

            var dto = new IncidenciaCreateDTO
            {
                TipoIncidencia = TipoIncidencia.Falta,
                Minuto = 30,
                JugadorId = jugadorAjenoId,
                EquipoId = otroEquipoId
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/partidos/{partidoId}/incidencias", dto);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
    }
}
