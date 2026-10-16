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
                Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8),
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
    }
}
