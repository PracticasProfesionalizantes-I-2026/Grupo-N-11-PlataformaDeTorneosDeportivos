using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
    public class EliminacionDirectaAvanceLlavesIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public EliminacionDirectaAvanceLlavesIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(Torneo Torneo, List<Equipo> Equipos)> CrearTorneoEliminacionDirectaConEquiposAsync(int cantidadEquipos)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Eliminación {cantidadEquipos} - {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.EliminacionDirecta,
                CupoMaximo = cantidadEquipos,
                CostoInscripcion = 0m,
                EsPago = false,
                Estado = EstadoTorneo.Abierto
            };

            db.Torneos.Add(torneo);

            var equipos = new List<Equipo>();
            for (int i = 0; i < cantidadEquipos; i++)
            {
                var eq = new Equipo
                {
                    Id = Guid.NewGuid(),
                    Nombre = $"Equipo {((char)('A' + i))} ({Guid.NewGuid().ToString().Substring(0, 4)})",
                    TorneoId = torneo.Id,
                    EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                    EdicionListaHabilitada = false
                };
                equipos.Add(eq);
                db.Equipos.Add(eq);
            }

            await db.SaveChangesAsync();
            return (torneo, equipos);
        }

        [Fact]
        public async Task Test1_Torneo4Equipos_AvanceDeSemifinalAFinal_CreaPartidoFinalAutomaticamente()
        {
            // 1. Crear torneo de Eliminación Directa con 4 equipos confirmados
            var (torneo, equipos) = await CrearTorneoEliminacionDirectaConEquiposAsync(4);

            // 2. Generar el fixture (crea los 2 partidos de Semifinal en Ronda 1)
            var fixtureDto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(2),
                Horarios = new List<string> { "10:00", "12:00" },
                Sedes = new List<string> { "Cancha Central", "Cancha 2" }
            };

            var fixtureResponse = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/generar-fixture", fixtureDto);
            Assert.Equal(HttpStatusCode.OK, fixtureResponse.StatusCode);

            // Obtener los partidos de Ronda 1 desde la base de datos
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var partidosRonda1 = await db.Partidos
                    .Where(p => p.TorneoId == torneo.Id && p.Ronda == 1)
                    .OrderBy(p => p.FechaHora)
                    .ToListAsync();

                Assert.Equal(2, partidosRonda1.Count);

                var semi1 = partidosRonda1[0];
                var semi2 = partidosRonda1[1];

                // 3. Cargar el resultado de Semifinal 1 (Gana el equipo local)
                var resultadoSemi1Dto = new CargarResultadoDTO { MarcadorLocal = 3, MarcadorVisitante = 1 };
                var respSemi1 = await _client.PutAsJsonAsync($"/api/partidos/{semi1.Id}/resultado", resultadoSemi1Dto);
                Assert.Equal(HttpStatusCode.OK, respSemi1.StatusCode);

                // Tras finalizar solo 1 semifinal, la final aún NO debe existir
                var partidosRonda2Intermedio = await db.Partidos.Where(p => p.TorneoId == torneo.Id && p.Ronda == 2).ToListAsync();
                Assert.Empty(partidosRonda2Intermedio);

                // 4. Cargar el resultado de Semifinal 2 (Gana el equipo visitante)
                var resultadoSemi2Dto = new CargarResultadoDTO { MarcadorLocal = 0, MarcadorVisitante = 2 };
                var respSemi2 = await _client.PutAsJsonAsync($"/api/partidos/{semi2.Id}/resultado", resultadoSemi2Dto);
                Assert.Equal(HttpStatusCode.OK, respSemi2.StatusCode);

                // 5. Verificar que el sistema creó/programó automáticamente el partido de la Ronda 2 (Final)
                var partidosRonda2 = await db.Partidos
                    .Where(p => p.TorneoId == torneo.Id && p.Ronda == 2)
                    .ToListAsync();

                Assert.Single(partidosRonda2);
                var partidoFinal = partidosRonda2[0];

                Assert.Equal(EstadoPartido.Pendiente, partidoFinal.Estado);
                Assert.Equal(semi1.EquipoLocalId, partidoFinal.EquipoLocalId);
                Assert.Equal(semi2.EquipoVisitanteId, partidoFinal.EquipoVisitanteId);

                // El torneo debe seguir en "EnDesarrollo"
                var torneoEnDb = await db.Torneos.FindAsync(torneo.Id);
                Assert.NotNull(torneoEnDb);
                Assert.Equal(EstadoTorneo.EnDesarrollo, torneoEnDb.Estado);
                Assert.Null(torneoEnDb.CampeonId);
            }
        }

        [Fact]
        public async Task Test2_Torneo4Equipos_PartidoFinalDeclaraCampeonYFinalizaTorneo()
        {
            // 1. Preparar torneo de 4 equipos y jugar semifinales
            var (torneo, _) = await CrearTorneoEliminacionDirectaConEquiposAsync(4);

            var fixtureDto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(1),
                Horarios = new List<string> { "14:00", "16:00" },
                Sedes = new List<string> { "Estadio Principal" }
            };

            var fixtureResp = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/generar-fixture", fixtureDto);
            Assert.Equal(HttpStatusCode.OK, fixtureResp.StatusCode);

            Guid finalId;
            Guid ganadorEsperado;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var semis = await db.Partidos
                    .Where(p => p.TorneoId == torneo.Id && p.Ronda == 1)
                    .OrderBy(p => p.FechaHora)
                    .ToListAsync();

                // Gana Local en Semi 1
                await _client.PutAsJsonAsync($"/api/partidos/{semis[0].Id}/resultado", new CargarResultadoDTO { MarcadorLocal = 2, MarcadorVisitante = 0 });
                // Gana Local en Semi 2
                await _client.PutAsJsonAsync($"/api/partidos/{semis[1].Id}/resultado", new CargarResultadoDTO { MarcadorLocal = 4, MarcadorVisitante = 1 });

                // Obtener el partido de la Final creado
                var final = await db.Partidos.FirstAsync(p => p.TorneoId == torneo.Id && p.Ronda == 2);
                finalId = final.Id;
                ganadorEsperado = final.EquipoLocalId!.Value;
            }

            // 2. Cargar el resultado del Partido Final (Gana el equipo Local)
            var resultadoFinalDto = new CargarResultadoDTO { MarcadorLocal = 2, MarcadorVisitante = 1 };
            var responseFinal = await _client.PutAsJsonAsync($"/api/partidos/{finalId}/resultado", resultadoFinalDto);
            Assert.Equal(HttpStatusCode.OK, responseFinal.StatusCode);

            // 3. Verificar que el partido final pasa a Finalizado y el torneo pasa a Finalizado con su Campeón
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var partidoFinalDb = await db.Partidos.FindAsync(finalId);
                Assert.NotNull(partidoFinalDb);
                Assert.Equal(EstadoPartido.Finalizado, partidoFinalDb.Estado);

                var torneoDb = await db.Torneos.FindAsync(torneo.Id);
                Assert.NotNull(torneoDb);
                Assert.Equal(EstadoTorneo.Finalizado, torneoDb.Estado);
                Assert.Equal(ganadorEsperado, torneoDb.CampeonId);
            }

            // 4. Verificar respuesta de la API GET /api/torneos/{id}
            var getTorneoResp = await _client.GetAsync($"/api/torneos/{torneo.Id}");
            Assert.Equal(HttpStatusCode.OK, getTorneoResp.StatusCode);
            var torneoDto = await getTorneoResp.Content.ReadFromJsonAsync<TorneoResponseDTO>();
            Assert.NotNull(torneoDto);
            Assert.Equal(EstadoTorneo.Finalizado.ToString(), torneoDto.Estado);
            Assert.Equal(ganadorEsperado, torneoDto.CampeonId);
            Assert.False(string.IsNullOrWhiteSpace(torneoDto.CampeonNombre));

            // 5. Verificar llaves GET /api/torneos/{id}/llaves
            var getLlavesResp = await _client.GetAsync($"/api/torneos/{torneo.Id}/llaves");
            Assert.Equal(HttpStatusCode.OK, getLlavesResp.StatusCode);
            var llavesDto = await getLlavesResp.Content.ReadFromJsonAsync<BracketTorneoDTO>();
            Assert.NotNull(llavesDto);
            Assert.Equal(2, llavesDto.TotalRondas);
            Assert.Equal(ganadorEsperado, llavesDto.CampeonId);
            Assert.Equal(torneoDto.CampeonNombre, llavesDto.CampeonNombre);
        }

        [Fact]
        public async Task Test3_Torneo8Equipos_ProgresionCompleta_Cuartos_Semifinal_Final_HastaFinalizado()
        {
            // 1. Crear torneo con 8 equipos confirmados
            var (torneo, _) = await CrearTorneoEliminacionDirectaConEquiposAsync(8);

            // 2. Generar fixture (crea 4 partidos de Cuartos de Final en Ronda 1)
            var fixtureDto = new GenerarFixtureRequestDTO
            {
                FechaInicio = DateTime.Today.AddDays(3),
                Horarios = new List<string> { "10:00", "12:00", "14:00", "16:00" },
                Sedes = new List<string> { "Cancha 1", "Cancha 2" }
            };

            var fixtureResp = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/generar-fixture", fixtureDto);
            Assert.Equal(HttpStatusCode.OK, fixtureResp.StatusCode);

            // 3. Jugar los 4 partidos de Cuartos de Final (Ronda 1)
            List<Partido> cuartos;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                cuartos = await db.Partidos
                    .Where(p => p.TorneoId == torneo.Id && p.Ronda == 1)
                    .OrderBy(p => p.FechaHora)
                    .ToListAsync();

                Assert.Equal(4, cuartos.Count);
            }

            // Cuartos 0 y 1 (alimentan Semifinal 1)
            await _client.PutAsJsonAsync($"/api/partidos/{cuartos[0].Id}/resultado", new CargarResultadoDTO { MarcadorLocal = 1, MarcadorVisitante = 0 });
            await _client.PutAsJsonAsync($"/api/partidos/{cuartos[1].Id}/resultado", new CargarResultadoDTO { MarcadorLocal = 2, MarcadorVisitante = 1 });

            // Cuartos 2 y 3 (alimentan Semifinal 2)
            await _client.PutAsJsonAsync($"/api/partidos/{cuartos[2].Id}/resultado", new CargarResultadoDTO { MarcadorLocal = 3, MarcadorVisitante = 0 });
            await _client.PutAsJsonAsync($"/api/partidos/{cuartos[3].Id}/resultado", new CargarResultadoDTO { MarcadorLocal = 0, MarcadorVisitante = 1 });

            // 4. Verificar que se crearon los 2 partidos de Semifinal (Ronda 2)
            List<Partido> semifinales;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                semifinales = await db.Partidos
                    .Where(p => p.TorneoId == torneo.Id && p.Ronda == 2)
                    .OrderBy(p => p.FechaHora)
                    .ToListAsync();

                Assert.Equal(2, semifinales.Count);
                Assert.All(semifinales, p => Assert.Equal(EstadoPartido.Pendiente, p.Estado));

                var torneoDb = await db.Torneos.FindAsync(torneo.Id);
                Assert.Equal(EstadoTorneo.EnDesarrollo, torneoDb!.Estado);
            }

            // 5. Jugar las 2 Semifinales (Ronda 2)
            await _client.PutAsJsonAsync($"/api/partidos/{semifinales[0].Id}/resultado", new CargarResultadoDTO { MarcadorLocal = 2, MarcadorVisitante = 1 });
            await _client.PutAsJsonAsync($"/api/partidos/{semifinales[1].Id}/resultado", new CargarResultadoDTO { MarcadorLocal = 1, MarcadorVisitante = 0 });

            // 6. Verificar que se creó el partido de la Final (Ronda 3)
            Partido final;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var finales = await db.Partidos
                    .Where(p => p.TorneoId == torneo.Id && p.Ronda == 3)
                    .ToListAsync();

                Assert.Single(finales);
                final = finales[0];
                Assert.Equal(EstadoPartido.Pendiente, final.Estado);
            }

            // 7. Jugar el partido Final (Ronda 3)
            var respFinal = await _client.PutAsJsonAsync($"/api/partidos/{final.Id}/resultado", new CargarResultadoDTO { MarcadorLocal = 3, MarcadorVisitante = 2 });
            Assert.Equal(HttpStatusCode.OK, respFinal.StatusCode);

            // 8. Verificar que el torneo quedó FINALIZADO con su CAMPEÓN
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneoFinalizado = await db.Torneos.FindAsync(torneo.Id);
                Assert.NotNull(torneoFinalizado);
                Assert.Equal(EstadoTorneo.Finalizado, torneoFinalizado.Estado);
                Assert.Equal(final.EquipoLocalId, torneoFinalizado.CampeonId);
            }
        }
    }
}

