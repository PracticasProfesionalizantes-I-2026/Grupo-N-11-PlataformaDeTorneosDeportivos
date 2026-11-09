using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using DataAccess.Data;
using DataAccess.Entities;
using Microsoft.Extensions.DependencyInjection;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    public class InscripcionesIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly CustomWebApplicationFactory<API.Program> _factory;
        private readonly HttpClient _client;

        public InscripcionesIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(Torneo torneo, Equipo equipo)> CreateTestTorneoAndEquipoAsync(
            string nombreTorneo,
            int cupoMaximo,
            bool esPago,
            decimal costo,
            EstadoInscripcion estadoEquipo = EstadoInscripcion.PendienteDeValidacion,
            bool pagoValidado = true)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = nombreTorneo,
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = cupoMaximo,
                EsPago = esPago,
                CostoInscripcion = costo,
                Estado = EstadoTorneo.Abierto
            };
            db.Torneos.Add(torneo);

            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = estadoEquipo,
                PagoValidado = pagoValidado
            };
            db.Equipos.Add(equipo);

            await db.SaveChangesAsync();
            return (torneo, equipo);
        }

        [Fact]
        public async Task AprobarInscripcion_WithValidTeam_Returns200Ok()
        {
            // Arrange
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Aprobar {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: false,
                costo: 0m);

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<AprobarInscripcionResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(equipo.Id, result.EquipoId);
            Assert.Equal(EstadoInscripcion.InscritoYConfirmado.ToString(), result.EstadoInscripcion);
            Assert.Equal(7, result.CuposDisponibles); // 8 - 1 = 7
        }

        [Fact]
        public async Task AprobarInscripcion_WhenLastSpotConfirmed_TransitionsTournamentToInscripcionesCerradas()
        {
            // Arrange - Torneo con CupoMaximo = 1 y 1 equipo pendiente
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Cierre Cupo {Guid.NewGuid():N}",
                cupoMaximo: 1,
                esPago: false,
                costo: 0m);

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<AprobarInscripcionResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(0, result.CuposDisponibles);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var torneoDb = await db.Torneos.FindAsync(torneo.Id);
            Assert.NotNull(torneoDb);
            Assert.Equal(EstadoTorneo.InscripcionesCerradas, torneoDb.Estado);
        }

        [Fact]
        public async Task AprobarInscripcion_WithMalformedIds_Returns400BadRequest()
        {
            // Arrange - IDs malformados (no son GUIDs)
            var response = await _client.PatchAsync(
                "/api/torneos/id-invalido/equipos/otro-invalido/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task AprobarInscripcion_WhenNotFound_Returns404NotFound()
        {
            // Arrange - GUIDs inexistentes
            var nonExistentTorneoId = Guid.NewGuid();
            var nonExistentEquipoId = Guid.NewGuid();

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{nonExistentTorneoId}/equipos/{nonExistentEquipoId}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task AprobarInscripcion_WhenTeamAlreadyProcessed_Returns409Conflict()
        {
            // Arrange - Equipo ya procesado (InscritoYConfirmado)
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Ya Procesado {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: false,
                costo: 0m,
                estadoEquipo: EstadoInscripcion.InscritoYConfirmado);

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task AprobarInscripcion_WhenTournamentFull_Returns409Conflict()
        {
            // Arrange - RN-06: Torneo con cupo máximo lleno (Cupo = 1, y ya hay 1 confirmado)
            var (torneo, equipoPendiente) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Lleno {Guid.NewGuid():N}",
                cupoMaximo: 1,
                esPago: false,
                costo: 0m,
                estadoEquipo: EstadoInscripcion.PendienteDeValidacion);

            // Crear un equipo ya confirmado en el mismo torneo
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Equipos.Add(new Equipo
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Equipo Ya Confirmado",
                    TorneoId = torneo.Id,
                    EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
                });
                await db.SaveChangesAsync();
            }

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipoPendiente.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task AprobarInscripcion_WhenUnpaidTournament_Returns422UnprocessableEntity()
        {
            // Arrange - RN-06: Torneo pago pero equipo sin comprobante de pago validado
            var (torneo, equipoSinPago) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Pago {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: true,
                costo: 1500m,
                estadoEquipo: EstadoInscripcion.PendienteDeValidacion,
                pagoValidado: false);

            // Act
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipoSinPago.Id}/aprobar",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task RechazarInscripcion_WithValidReason_Returns200Ok()
        {
            // Arrange - RN-03: Rechazo válido con motivo
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Rechazar {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: false,
                costo: 0m);

            var dto = new RechazarInscripcionDTO
            {
                MotivoRechazo = "La nómina presenta DNIs ilegibles en las credenciales."
            };

            // Act
            var response = await _client.PatchAsJsonAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/rechazar",
                dto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<RechazarInscripcionResponseDTO>();
            Assert.NotNull(result);
            Assert.Equal(equipo.Id, result.EquipoId);
            Assert.Equal(EstadoInscripcion.RechazadoObservado.ToString(), result.EstadoInscripcion);
            Assert.Equal(dto.MotivoRechazo, result.MotivoRechazo);
            Assert.True(result.EdicionListaHabilitada); // RN-03: edición desbloqueada
        }

        [Fact]
        public async Task RechazarInscripcion_WithEmptyReason_Returns400BadRequest()
        {
            // Arrange - Rechazo sin motivo (violación de [Required])
            var (torneo, equipo) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Rechazar Vacio {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: false,
                costo: 0m);

            var dto = new RechazarInscripcionDTO
            {
                MotivoRechazo = "" // Inválido
            };

            // Act
            var response = await _client.PatchAsJsonAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipo.Id}/rechazar",
                dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Test1_CrearEquipoConComprobanteUrl_PersisteComprobanteYIniciaConPagoValidadoFalse()
        {
            // Arrange
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Pago {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 10000m,
                Estado = EstadoTorneo.Abierto
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(torneo);
                await db.SaveChangesAsync();
            }

            var comprobanteEsperado = "https://comprobantes.com/pago123.pdf";
            var dto = new EquipoCreateDTO
            {
                Nombre = $"Equipo Con Pago {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                ComprobanteUrl = comprobanteEsperado,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Lionel", Apellido = "Messi", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Angel", Apellido = "DiMaria", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Rodrigo", Apellido = "DePaul", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Emiliano", Apellido = "Martinez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Julian", Apellido = "Alvarez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" }
                }
            };

            // Act - POST /api/torneos/{torneoId}/equipos
            var createResponse = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var createdEquipo = await createResponse.Content.ReadFromJsonAsync<EquipoResponseDTO>();
            Assert.NotNull(createdEquipo);
            Assert.Equal(comprobanteEsperado, createdEquipo.ComprobanteUrl);
            Assert.False(createdEquipo.PagoValidado);

            // Verificar persistencia en base de datos
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var equipoEnDb = await db.Equipos.FindAsync(createdEquipo.Id);
                Assert.NotNull(equipoEnDb);
                Assert.Equal(comprobanteEsperado, equipoEnDb.ComprobanteUrl);
                Assert.False(equipoEnDb.PagoValidado);
            }

            // Verificar endpoint GET /api/equipos/{id}
            var getResponse = await _client.GetAsync($"/api/equipos/{createdEquipo.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var getEquipo = await getResponse.Content.ReadFromJsonAsync<EquipoResponseDTO>();
            Assert.NotNull(getEquipo);
            Assert.Equal(comprobanteEsperado, getEquipo.ComprobanteUrl);
            Assert.False(getEquipo.PagoValidado);
        }

        [Fact]
        public async Task Test2_BloqueoAprobacionSinPagoValidado_EnTorneoArancelado_Retorna422UnprocessableEntity()
        {
            // Arrange - Torneo con arancel (EsPago = true, CostoInscripcion = 10000)
            var (torneo, equipoSinPago) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Arancelado {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: true,
                costo: 10000m,
                estadoEquipo: EstadoInscripcion.PendienteDeValidacion,
                pagoValidado: false);

            // Act - Intentar aprobar al equipo antes de validar su pago
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipoSinPago.Id}/aprobar",
                null);

            // Assert - Debe responder 422 Unprocessable Entity (PagoRequeridoException)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task Test3_FlujoCompletoLegitimoEnTorneoPago_ValidarPagoYAprobar_Retorna200Ok()
        {
            // 1. Crear torneo pago
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Flujo Completo {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 10000m,
                Estado = EstadoTorneo.Abierto
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(torneo);
                await db.SaveChangesAsync();
            }

            // 2. Inscribir equipo con ComprobanteUrl válido
            var comprobanteUrl = "https://banco.com/transferencia_valida.pdf";
            var dto = new EquipoCreateDTO
            {
                Nombre = $"Equipo Campeon {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                ComprobanteUrl = comprobanteUrl,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Lionel", Apellido = "Messi", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Angel", Apellido = "DiMaria", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Rodrigo", Apellido = "DePaul", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Emiliano", Apellido = "Martinez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Julian", Apellido = "Alvarez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" }
                }
            };

            var createResponse = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dto);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var createdEquipo = await createResponse.Content.ReadFromJsonAsync<EquipoResponseDTO>();
            Assert.NotNull(createdEquipo);
            Assert.False(createdEquipo.PagoValidado);

            // 3. El Organizador verifica el comprobante y valida el pago
            var validarPagoResponse = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{createdEquipo.Id}/validar-pago",
                null);

            Assert.Equal(HttpStatusCode.OK, validarPagoResponse.StatusCode);
            var validarPagoResult = await validarPagoResponse.Content.ReadFromJsonAsync<ValidarPagoResponseDTO>();
            Assert.NotNull(validarPagoResult);
            Assert.True(validarPagoResult.PagoValidado);
            Assert.Equal(comprobanteUrl, validarPagoResult.ComprobanteUrl);

            // Comprobar estado en base de datos: PagoValidado == true
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var equipoDb = await db.Equipos.FindAsync(createdEquipo.Id);
                Assert.NotNull(equipoDb);
                Assert.True(equipoDb.PagoValidado);
            }

            // 4. El Organizador aprueba la inscripción -> Debe responder 200 OK y transicionar a InscritoYConfirmado
            var aprobarResponse = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{createdEquipo.Id}/aprobar",
                null);

            Assert.Equal(HttpStatusCode.OK, aprobarResponse.StatusCode);
            var aprobarResult = await aprobarResponse.Content.ReadFromJsonAsync<AprobarInscripcionResponseDTO>();
            Assert.NotNull(aprobarResult);
            Assert.Equal(EstadoInscripcion.InscritoYConfirmado.ToString(), aprobarResult.EstadoInscripcion);

            // Verificar estado final en base de datos
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var equipoFinalDb = await db.Equipos.FindAsync(createdEquipo.Id);
                Assert.NotNull(equipoFinalDb);
                Assert.Equal(EstadoInscripcion.InscritoYConfirmado, equipoFinalDb.EstadoInscripcion);
                Assert.False(equipoFinalDb.EdicionListaHabilitada);
            }
        }

        [Fact]
        public async Task ValidarPago_TorneoPagoSinComprobanteCargado_Retorna422UnprocessableEntity()
        {
            // Arrange - Equipo en torneo pago sin comprobante cargado
            var (torneo, equipoSinComprobante) = await CreateTestTorneoAndEquipoAsync(
                $"Torneo Sin Comp {Guid.NewGuid():N}",
                cupoMaximo: 8,
                esPago: true,
                costo: 5000m,
                estadoEquipo: EstadoInscripcion.PendienteDeValidacion,
                pagoValidado: false);

            // Act - Intentar validar pago sin que exista comprobante
            var response = await _client.PatchAsync(
                $"/api/torneos/{torneo.Id}/equipos/{equipoSinComprobante.Id}/validar-pago",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task ValidarPago_RutaEquiposDirecta_Retorna200Ok()
        {
            // Arrange - Verificar endpoint alternativo PATCH /api/equipos/{id}/validar-pago
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Directo {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 3000m,
                Estado = EstadoTorneo.Abierto
            };

            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = "Equipo Directo",
                TorneoId = torneo.Id,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                ComprobanteUrl = "https://pagos.com/recibo.png",
                PagoValidado = false
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                await db.SaveChangesAsync();
            }

            // Act
            var response = await _client.PatchAsync(
                $"/api/equipos/{equipo.Id}/validar-pago",
                null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ValidarPagoResponseDTO>();
            Assert.NotNull(result);
            Assert.True(result.PagoValidado);
        }
    }
}
