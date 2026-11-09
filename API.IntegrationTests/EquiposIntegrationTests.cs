using System;
using System.Collections.Generic;
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
    public class EquiposIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly CustomWebApplicationFactory<API.Program> _factory;
        private readonly HttpClient _client;

        public EquiposIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<Torneo> CreateTestTorneoAsync(string nombre, Disciplina disciplina, EstadoTorneo estado = EstadoTorneo.Abierto)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = nombre,
                Disciplina = disciplina,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                CostoInscripcion = 1000m,
                Estado = estado
            };
            db.Torneos.Add(torneo);
            await db.SaveChangesAsync();
            return torneo;
        }

        [Fact]
        public async Task CreateEquipo_WithValidPayload_Returns201Created()
        {
            // Arrange
            var torneo = await CreateTestTorneoAsync($"Torneo Valido {Guid.NewGuid():N}", Disciplina.Futbol);
            var dto = new EquipoCreateDTO
            {
                Nombre = $"Equipo Halcones {Guid.NewGuid():N}",
                LogoUrl = "https://example.com/logo.png",
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = $"{Random.Shared.Next(10000000, 99999999)}" }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);

            var created = await response.Content.ReadFromJsonAsync<EquipoResponseDTO>();
            Assert.NotNull(created);
            Assert.Equal(dto.Nombre, created.Nombre);
            Assert.Equal(EstadoInscripcion.PendienteDeValidacion.ToString(), created.EstadoInscripcion);
            Assert.Equal(5, created.CantidadJugadores);
            Assert.Equal(torneo.Id, created.TorneoId);
        }

        [Fact]
        public async Task CreateEquipo_WithMalformedJson_Returns400BadRequest()
        {
            // Arrange - JSON con sintaxis corrupta
            var corruptContent = new StringContent("{ \"nombre\": \"Equipo Corrupto\", ", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"/api/torneos/{Guid.NewGuid()}/equipos", corruptContent);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateEquipo_WithMissingRequiredFields_Returns400BadRequest()
        {
            // Arrange - Nombre vacío y sin jugadores
            var torneo = await CreateTestTorneoAsync($"Torneo Campos {Guid.NewGuid():N}", Disciplina.Futbol);
            var dto = new EquipoCreateDTO
            {
                Nombre = "", // Inválido por [Required]
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>() // Lista vacía
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateEquipo_WhenTorneoNotFound_Returns404NotFound()
        {
            // Arrange - GUID inexistente
            var nonExistentTorneoId = Guid.NewGuid();
            var dto = new EquipoCreateDTO
            {
                Nombre = "Equipo Torneo Inexistente",
                TorneoId = nonExistentTorneoId,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "12345678" }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{nonExistentTorneoId}/equipos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateEquipo_WhenTorneoClosed_Returns409Conflict()
        {
            // Arrange - Torneo con inscripciones cerradas
            var torneo = await CreateTestTorneoAsync($"Torneo Cerrado {Guid.NewGuid():N}", Disciplina.Futbol, estado: EstadoTorneo.InscripcionesCerradas);
            var dto = new EquipoCreateDTO
            {
                Nombre = $"Equipo Tardio {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "11111111" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = "22222222" },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = "33333333" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = "44444444" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = "55555555" }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task CreateEquipo_WhenPlayersBelowMinimum_Returns422UnprocessableEntity()
        {
            // Arrange - Futbol exige 5 jugadores; enviamos 2
            var torneo = await CreateTestTorneoAsync($"Torneo Minimo {Guid.NewGuid():N}", Disciplina.Futbol);
            var dto = new EquipoCreateDTO
            {
                Nombre = $"Equipo Menos Jugadores {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "11111111" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = "22222222" }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task CreateEquipo_WhenDuplicateDniInList_Returns400BadRequest()
        {
            // Arrange - Dos jugadores con el mismo DNI "44444444"
            var torneo = await CreateTestTorneoAsync($"Torneo Dnis {Guid.NewGuid():N}", Disciplina.Futbol);
            var dto = new EquipoCreateDTO
            {
                Nombre = $"Equipo Dni Duplicado {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = "44444444" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = "44444444" }, // Duplicado
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = "33333333" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = "55555555" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = "66666666" }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateEquipo_WhenPlayerAlreadyRegisteredInTournament_Returns409Conflict()
        {
            // Arrange - RN-02: Registrar equipo 1 con DNI "77777777"
            var torneo = await CreateTestTorneoAsync($"Torneo RN02 {Guid.NewGuid():N}", Disciplina.Futbol);
            var dniCompartido = "77777777";

            var dtoEquipo1 = new EquipoCreateDTO
            {
                Nombre = $"Equipo Primero {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Uno", Dni = dniCompartido },
                    new() { Nombre = "Carlos", Apellido = "Dos", Dni = "10000001" },
                    new() { Nombre = "Lucas", Apellido = "Tres", Dni = "10000002" },
                    new() { Nombre = "Marcos", Apellido = "Cuatro", Dni = "10000003" },
                    new() { Nombre = "Diego", Apellido = "Cinco", Dni = "10000004" }
                }
            };

            var primerResponse = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dtoEquipo1);
            Assert.Equal(HttpStatusCode.Created, primerResponse.StatusCode);

            // Act - Intentar registrar equipo 2 con el mismo jugador en el mismo torneo
            var dtoEquipo2 = new EquipoCreateDTO
            {
                Nombre = $"Equipo Segundo {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "JuanCopia", Apellido = "UnoCopia", Dni = dniCompartido }, // Violación RN-02
                    new() { Nombre = "Pedro", Apellido = "Seis", Dni = "20000001" },
                    new() { Nombre = "Pablo", Apellido = "Siete", Dni = "20000002" },
                    new() { Nombre = "Mateo", Apellido = "Ocho", Dni = "20000003" },
                    new() { Nombre = "Tomas", Apellido = "Nueve", Dni = "20000004" }
                }
            };

            var segundoResponse = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dtoEquipo2);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, segundoResponse.StatusCode);
        }

        [Fact]
        public async Task CreateEquipo_WhenDuplicateTeamName_Returns409Conflict()
        {
            // Arrange - Crear primer equipo con un nombre específico
            var torneo = await CreateTestTorneoAsync($"Torneo Nombre Duplicado {Guid.NewGuid():N}", Disciplina.Futbol);
            var nombreRepetido = $"Equipo Clon {Guid.NewGuid():N}";

            var dtoEquipo1 = new EquipoCreateDTO
            {
                Nombre = nombreRepetido,
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Uno", Dni = "30000001" },
                    new() { Nombre = "Carlos", Apellido = "Dos", Dni = "30000002" },
                    new() { Nombre = "Lucas", Apellido = "Tres", Dni = "30000003" },
                    new() { Nombre = "Marcos", Apellido = "Cuatro", Dni = "30000004" },
                    new() { Nombre = "Diego", Apellido = "Cinco", Dni = "30000005" }
                }
            };

            var primerResponse = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dtoEquipo1);
            Assert.Equal(HttpStatusCode.Created, primerResponse.StatusCode);

            // Act - Intentar crear otro equipo con el mismo nombre en el mismo torneo
            var dtoEquipo2 = new EquipoCreateDTO
            {
                Nombre = nombreRepetido,
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Pedro", Apellido = "Seis", Dni = "40000001" },
                    new() { Nombre = "Pablo", Apellido = "Siete", Dni = "40000002" },
                    new() { Nombre = "Mateo", Apellido = "Ocho", Dni = "40000003" },
                    new() { Nombre = "Tomas", Apellido = "Nueve", Dni = "40000004" },
                    new() { Nombre = "Nicolas", Apellido = "Diez", Dni = "40000005" }
                }
            };

            var segundoResponse = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dtoEquipo2);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, segundoResponse.StatusCode);
        }

        [Fact]
        public async Task CreateEquipo_WhenTournamentFull_Returns409Conflict()
        {
            // Arrange - Torneo con CupoMaximo = 1 y ya hay 1 equipo confirmado
            var torneo = await CreateTestTorneoAsync($"Torneo Cupo Lleno {Guid.NewGuid():N}", Disciplina.Futbol);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var t = await db.Torneos.FindAsync(torneo.Id);
                t!.CupoMaximo = 1;
                db.Equipos.Add(new Equipo
                {
                    Id = Guid.NewGuid(),
                    Nombre = $"Equipo Confirmado {Guid.NewGuid():N}",
                    TorneoId = torneo.Id,
                    EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
                });
                await db.SaveChangesAsync();
            }

            var dto = new EquipoCreateDTO
            {
                Nombre = $"Equipo Excedente {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = $"{Random.Shared.Next(10000000, 99999999)}" }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task GetEstadisticasEquipo_Returns200OK()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = await CreateTestTorneoAsync($"Torneo Stats Equipo {Guid.NewGuid():N}", Disciplina.Futbol);
            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo Stats {Guid.NewGuid():N}",
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                TorneoId = torneo.Id
            };
            db.Equipos.Add(equipo);

            var jugador = new Jugador
            {
                Id = Guid.NewGuid(),
                Nombre = "Enzo",
                Apellido = "Fernandez",
                Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                EquipoId = equipo.Id
            };
            db.Jugadores.Add(jugador);

            var rival = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Rival {Guid.NewGuid():N}",
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado,
                TorneoId = torneo.Id
            };
            db.Equipos.Add(rival);

            var partido = new Partido
            {
                Id = Guid.NewGuid(),
                TorneoId = torneo.Id,
                EquipoLocalId = equipo.Id,
                EquipoVisitanteId = rival.Id,
                MarcadorLocal = 3,
                MarcadorVisitante = 1,
                Estado = EstadoPartido.Finalizado,
                FechaHora = DateTime.UtcNow,
                Sede = "Estadio Principal",
                Ronda = 1
            };
            db.Partidos.Add(partido);

            await db.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/equipos/{equipo.Id}/estadisticas");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var stats = await response.Content.ReadFromJsonAsync<EquipoEstadisticasDTO>();
            Assert.NotNull(stats);
            Assert.Equal(equipo.Id, stats.EquipoId);
            Assert.Equal(1, stats.PartidosJugados);
            Assert.Equal(1, stats.Victorias);
            Assert.Equal(3, stats.GolesAFavor);
            Assert.Equal(1, stats.GolesEnContra);
            Assert.Equal(100.0, stats.Efectividad);
            Assert.Single(stats.Integrantes);
        }

        [Fact]
        public async Task GetEstadisticasEquipo_WhenDoesNotExist_Returns404NotFound()
        {
            // Act
            var response = await _client.GetAsync($"/api/equipos/{Guid.NewGuid()}/estadisticas");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateEquipo_WhenUserIsVeedor_ShouldReturn403Forbidden()
        {
            // Arrange - Crear torneo
            var torneo = await CreateTestTorneoAsync($"Torneo Veedor Test {Guid.NewGuid():N}", Disciplina.Futbol);

            // Crear un usuario con rol de Veedor en la base de datos
            var veedorEmail = $"veedor_{Guid.NewGuid():N}@torneos.com";
            var password = "Password123!";
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<BusinessLogic.Security.IPasswordHasher>();

                db.Usuarios.Add(new Usuario
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Roberto",
                    Apellido = "Veedor",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    Email = veedorEmail,
                    PasswordHash = hasher.HashPassword(password),
                    Rol = RolUsuario.Veedor,
                    Activo = true
                });
                await db.SaveChangesAsync();
            }

            // Iniciar sesión como Veedor para obtener su token JWT
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDTO
            {
                EmailOrDni = veedorEmail,
                Password = password
            });
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDTO>();
            Assert.NotNull(loginResult);
            Assert.Equal("Veedor", loginResult.Rol);

            var dto = new EquipoCreateDTO
            {
                Nombre = $"Equipo Veedor {Guid.NewGuid():N}",
                LogoUrl = "https://example.com/logo.png",
                TorneoId = torneo.Id,
                Jugadores = new List<JugadorCreateDTO>
                {
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = $"{Random.Shared.Next(10000000, 99999999)}" },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = $"{Random.Shared.Next(10000000, 99999999)}" }
                }
            };

            // Act - El Veedor intenta crear un equipo con su token
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/torneos/{torneo.Id}/equipos");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult.Token);
            request.Content = JsonContent.Create(dto);

            var response = await _client.SendAsync(request);

            // Assert - Debe responder 403 Forbidden (no permitido a Veedores)
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CambiarEstado_TorneoPagoSinPagoValidado_Retorna422UnprocessableEntity()
        {
            // Arrange - Torneo pago
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Pago {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 10,
                EsPago = true,
                CostoInscripcion = 2000m,
                Estado = EstadoTorneo.Abierto
            };

            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo Impago {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                PagoValidado = false,
                EdicionListaHabilitada = true
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                db.Jugadores.Add(new Jugador
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Carlos",
                    Apellido = "Tevez",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    EquipoId = equipo.Id
                });
                await db.SaveChangesAsync();
            }

            // Act - Intentar pasar a InscritoYConfirmado sin pago validado
            var response = await _client.PutAsJsonAsync($"/api/equipos/{equipo.Id}/estado", "InscritoYConfirmado");

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task CambiarEstado_TorneoLleno_Retorna409Conflict()
        {
            // Arrange - Torneo con cupo 2 ya lleno
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Lleno {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 2,
                EsPago = false,
                CostoInscripcion = 0m,
                Estado = EstadoTorneo.Abierto
            };

            var eq1 = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Eq1 {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
            };
            var eq2 = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Eq2 {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
            };
            var eq3 = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Eq3 {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                PagoValidado = true
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(torneo);
                db.Equipos.AddRange(eq1, eq2, eq3);
                db.Jugadores.Add(new Jugador
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Roman",
                    Apellido = "Riquelme",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    EquipoId = eq3.Id
                });
                await db.SaveChangesAsync();
            }

            // Act - Intentar confirmar el 3er equipo excediendo el cupo
            var response = await _client.PutAsJsonAsync($"/api/equipos/{eq3.Id}/estado", "InscritoYConfirmado");

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task CambiarEstado_EquipoYaConfirmado_Retorna409Conflict()
        {
            // Arrange
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Estado {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0m,
                Estado = EstadoTorneo.Abierto
            };

            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo Ya Confirmado {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                await db.SaveChangesAsync();
            }

            // Act - Intentar re-confirmar o transicionar equipo ya confirmado
            var response = await _client.PutAsJsonAsync($"/api/equipos/{equipo.Id}/estado", "InscritoYConfirmado");

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task CambiarEstado_AprobarExitoso_Retorna200OKYDeshabilitaEdicion()
        {
            // Arrange
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Exitoso {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = true,
                CostoInscripcion = 1000m,
                Estado = EstadoTorneo.Abierto
            };

            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo A Confirmar {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                PagoValidado = true,
                ComprobanteUrl = "https://example.com/pago.pdf",
                EdicionListaHabilitada = true
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                db.Jugadores.Add(new Jugador
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Lionel",
                    Apellido = "Messi",
                    Dni = $"{Random.Shared.Next(10000000, 99999999)}",
                    EquipoId = equipo.Id
                });
                await db.SaveChangesAsync();
            }

            // Act
            var response = await _client.PutAsJsonAsync($"/api/equipos/{equipo.Id}/estado", "InscritoYConfirmado");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await response.Content.ReadFromJsonAsync<EquipoResponseDTO>();
            Assert.NotNull(updated);
            Assert.Equal(EstadoInscripcion.InscritoYConfirmado.ToString(), updated.EstadoInscripcion);

            // Verificar persistencia en base de datos
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var equipoEnDb = await db.Equipos.FindAsync(equipo.Id);
                Assert.NotNull(equipoEnDb);
                Assert.Equal(EstadoInscripcion.InscritoYConfirmado, equipoEnDb.EstadoInscripcion);
                Assert.False(equipoEnDb.EdicionListaHabilitada);
            }
        }

        [Fact]
        public async Task CambiarEstado_RechazarObservar_Retorna200OKYHabilitaEdicion()
        {
            // Arrange
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Rechazo {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                EsPago = false,
                CostoInscripcion = 0m,
                Estado = EstadoTorneo.Abierto
            };

            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo A Observar {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                EdicionListaHabilitada = false
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                await db.SaveChangesAsync();
            }

            // Act
            var response = await _client.PutAsJsonAsync($"/api/equipos/{equipo.Id}/estado", "RechazadoObservado");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await response.Content.ReadFromJsonAsync<EquipoResponseDTO>();
            Assert.NotNull(updated);
            Assert.Equal(EstadoInscripcion.RechazadoObservado.ToString(), updated.EstadoInscripcion);

            // Verificar persistencia en base de datos
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var equipoEnDb = await db.Equipos.FindAsync(equipo.Id);
                Assert.NotNull(equipoEnDb);
                Assert.Equal(EstadoInscripcion.RechazadoObservado, equipoEnDb.EstadoInscripcion);
                Assert.True(equipoEnDb.EdicionListaHabilitada);
            }
        }

        [Fact]
        public async Task Test2_ConsultaPublicaAnonima_OcultaDniYDocumentacionUrl()
        {
            // Arrange (RNF-04: Privacidad en consultas públicas)
            var torneo = await CreateTestTorneoAsync($"Torneo Privacidad {Guid.NewGuid():N}", Disciplina.Futbol);
            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo Publico {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Equipos.Add(equipo);
                db.Jugadores.Add(new Jugador
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Lautaro",
                    Apellido = "Martinez",
                    Dni = "33444555",
                    DocumentacionUrl = "https://confidencial.com/deslinde.pdf",
                    EquipoId = equipo.Id
                });
                await db.SaveChangesAsync();
            }

            // Act 1: GET /api/equipos/{id} anónimo
            var reqDetalle = new HttpRequestMessage(HttpMethod.Get, $"/api/equipos/{equipo.Id}");
            reqDetalle.Headers.Add("X-Test-Anonymous", "true");
            var resDetalle = await _client.SendAsync(reqDetalle);

            // Assert 1: Responde 200 OK y el JSON NO expone dni ni documentacionUrl
            Assert.Equal(HttpStatusCode.OK, resDetalle.StatusCode);
            var jsonDetalle = await resDetalle.Content.ReadAsStringAsync();
            Assert.Contains("Lautaro", jsonDetalle);
            Assert.Contains("Martinez", jsonDetalle);
            Assert.DoesNotContain("\"dni\"", jsonDetalle, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"documentacionurl\"", jsonDetalle, StringComparison.OrdinalIgnoreCase);

            // Act 2: GET /api/equipos anónimo
            var reqListado = new HttpRequestMessage(HttpMethod.Get, "/api/equipos");
            reqListado.Headers.Add("X-Test-Anonymous", "true");
            var resListado = await _client.SendAsync(reqListado);

            // Assert 2: Responde 200 OK y el JSON NO expone dni ni documentacionUrl
            Assert.Equal(HttpStatusCode.OK, resListado.StatusCode);
            var jsonListado = await resListado.Content.ReadAsStringAsync();
            Assert.DoesNotContain("\"dni\"", jsonListado, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"documentacionurl\"", jsonListado, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Test3_AccesoAutorizado_OrganizadorYCapitanPropio_AccedenADniYDocumentacion()
        {
            // Arrange
            var torneo = await CreateTestTorneoAsync($"Torneo PII Auth {Guid.NewGuid():N}", Disciplina.Futbol);
            var capitanId = Guid.NewGuid();
            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo Con Capitan {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                CapitanId = capitanId,
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
            };

            var dniSensible = "38999888";
            var docSensible = "https://privado.com/documentacion_secreta.pdf";

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Equipos.Add(equipo);
                db.Jugadores.Add(new Jugador
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Enzo",
                    Apellido = "Fernandez",
                    Dni = dniSensible,
                    DocumentacionUrl = docSensible,
                    EquipoId = equipo.Id
                });
                await db.SaveChangesAsync();
            }

            // Act 1: Organizador consulta GET /api/equipos/{id}/nomina-completa
            var reqOrg = new HttpRequestMessage(HttpMethod.Get, $"/api/equipos/{equipo.Id}/nomina-completa");
            reqOrg.Headers.Add("X-Test-Role", "Organizador");
            var resOrg = await _client.SendAsync(reqOrg);

            // Assert 1: Acceso completo concedido
            Assert.Equal(HttpStatusCode.OK, resOrg.StatusCode);
            var jsonOrg = await resOrg.Content.ReadAsStringAsync();
            Assert.Contains(dniSensible, jsonOrg);
            Assert.Contains(docSensible, jsonOrg);

            // Act 2: El propio Capitán consulta GET /api/equipos/{id}/nomina-completa
            var reqCap = new HttpRequestMessage(HttpMethod.Get, $"/api/equipos/{equipo.Id}/nomina-completa");
            reqCap.Headers.Add("X-Test-Role", "Capitan");
            reqCap.Headers.Add("X-Test-UserId", capitanId.ToString());
            var resCap = await _client.SendAsync(reqCap);

            // Assert 2: Acceso completo concedido a su propio capitán
            Assert.Equal(HttpStatusCode.OK, resCap.StatusCode);
            var jsonCap = await resCap.Content.ReadAsStringAsync();
            Assert.Contains(dniSensible, jsonCap);
            Assert.Contains(docSensible, jsonCap);
        }

        [Fact]
        public async Task Test4_Bloqueo403Forbidden_ParaOtroUsuarioNoAutorizado()
        {
            // Arrange - Equipo perteneciente a Capitan 1
            var torneo = await CreateTestTorneoAsync($"Torneo PII Bloqueo {Guid.NewGuid():N}", Disciplina.Futbol);
            var capitan1 = Guid.NewGuid();
            var capitan2 = Guid.NewGuid();
            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Equipo Protegido {Guid.NewGuid():N}",
                TorneoId = torneo.Id,
                CapitanId = capitan1,
                EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado
            };

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Equipos.Add(equipo);
                db.Jugadores.Add(new Jugador
                {
                    Id = Guid.NewGuid(),
                    Nombre = "Alexis",
                    Apellido = "MacAllister",
                    Dni = "40111222",
                    DocumentacionUrl = "https://privado.com/doc.pdf",
                    EquipoId = equipo.Id
                });
                await db.SaveChangesAsync();
            }

            // Act 1: Capitan 2 intenta acceder a la nómina completa del Equipo de Capitan 1
            var reqOtroCap = new HttpRequestMessage(HttpMethod.Get, $"/api/equipos/{equipo.Id}/nomina-completa");
            reqOtroCap.Headers.Add("X-Test-Role", "Capitan");
            reqOtroCap.Headers.Add("X-Test-UserId", capitan2.ToString());
            var resOtroCap = await _client.SendAsync(reqOtroCap);

            // Assert 1: Debe responder 403 Forbidden
            Assert.Equal(HttpStatusCode.Forbidden, resOtroCap.StatusCode);

            // Act 2: Veedor intenta acceder a la nómina completa
            var reqVeedor = new HttpRequestMessage(HttpMethod.Get, $"/api/equipos/{equipo.Id}/nomina-completa");
            reqVeedor.Headers.Add("X-Test-Role", "Veedor");
            reqVeedor.Headers.Add("X-Test-UserId", Guid.NewGuid().ToString());
            var resVeedor = await _client.SendAsync(reqVeedor);

            // Assert 2: Debe responder 403 Forbidden
            Assert.Equal(HttpStatusCode.Forbidden, resVeedor.StatusCode);

            // Act 3: Usuario anónimo intenta acceder a endpoint protegido
            var reqAnon = new HttpRequestMessage(HttpMethod.Get, $"/api/equipos/{equipo.Id}/nomina-completa");
            reqAnon.Headers.Add("X-Test-Anonymous", "true");
            var resAnon = await _client.SendAsync(reqAnon);

            // Assert 3: Debe responder 401 Unauthorized
            Assert.Equal(HttpStatusCode.Unauthorized, resAnon.StatusCode);
        }
    }
}

