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
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) }
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
                    new() { Nombre = "A", Apellido = "B", Dni = "12345678" }
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
                    new() { Nombre = "A", Apellido = "Uno", Dni = dniCompartido },
                    new() { Nombre = "B", Apellido = "Dos", Dni = "10000001" },
                    new() { Nombre = "C", Apellido = "Tres", Dni = "10000002" },
                    new() { Nombre = "D", Apellido = "Cuatro", Dni = "10000003" },
                    new() { Nombre = "E", Apellido = "Cinco", Dni = "10000004" }
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
                    new() { Nombre = "A_Copia", Apellido = "Uno_Copia", Dni = dniCompartido }, // Violación RN-02
                    new() { Nombre = "F", Apellido = "Seis", Dni = "20000001" },
                    new() { Nombre = "G", Apellido = "Siete", Dni = "20000002" },
                    new() { Nombre = "H", Apellido = "Ocho", Dni = "20000003" },
                    new() { Nombre = "I", Apellido = "Nueve", Dni = "20000004" }
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
                    new() { Nombre = "A", Apellido = "Uno", Dni = "30000001" },
                    new() { Nombre = "B", Apellido = "Dos", Dni = "30000002" },
                    new() { Nombre = "C", Apellido = "Tres", Dni = "30000003" },
                    new() { Nombre = "D", Apellido = "Cuatro", Dni = "30000004" },
                    new() { Nombre = "E", Apellido = "Cinco", Dni = "30000005" }
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
                    new() { Nombre = "F", Apellido = "Seis", Dni = "40000001" },
                    new() { Nombre = "G", Apellido = "Siete", Dni = "40000002" },
                    new() { Nombre = "H", Apellido = "Ocho", Dni = "40000003" },
                    new() { Nombre = "I", Apellido = "Nueve", Dni = "40000004" },
                    new() { Nombre = "J", Apellido = "Diez", Dni = "40000005" }
                }
            };

            var segundoResponse = await _client.PostAsJsonAsync($"/api/torneos/{torneo.Id}/equipos", dtoEquipo2);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, segundoResponse.StatusCode);
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
                Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8),
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
                    new() { Nombre = "Juan", Apellido = "Perez", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) },
                    new() { Nombre = "Carlos", Apellido = "Lopez", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) },
                    new() { Nombre = "Lucas", Apellido = "Gomez", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) },
                    new() { Nombre = "Marcos", Apellido = "Diaz", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) },
                    new() { Nombre = "Diego", Apellido = "Romero", Dni = $"{Guid.NewGuid().GetHashCode():N}".Substring(0, 8) }
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
    }
}

