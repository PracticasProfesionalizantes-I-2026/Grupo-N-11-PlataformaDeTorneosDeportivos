using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Middlewares;
using DataAccess.Data;
using DataAccess.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.DTOs;
using Shared.Enums;
using Xunit;

namespace API.IntegrationTests
{
    /// <summary>
    /// Pruebas dedicadas a validar el Control de Concurrencia Optimista en Torneos (Cupo Máximo)
    /// y su captura en ExceptionHandlingMiddleware respondiendo 409 Conflict.
    /// </summary>
    public class TorneoConcurrenciaOptimistaIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public TorneoConcurrenciaOptimistaIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        #region Criterio 1: Configuración de Concurrencia Optimista en Entidad Torneo

        [Fact]
        public void Torneo_TieneAtributosTimestampYConcurrencyCheckEnRowVersion()
        {
            // Assert sobre la propiedad y sus atributos
            var prop = typeof(Torneo).GetProperty(nameof(Torneo.RowVersion));
            Assert.NotNull(prop);
            Assert.Equal(typeof(byte[]), prop.PropertyType);

            var hasTimestamp = prop.GetCustomAttributes(typeof(TimestampAttribute), true).Any();
            Assert.True(hasTimestamp, "La propiedad 'RowVersion' debe poseer el atributo [Timestamp].");

            var hasConcurrencyCheck = prop.GetCustomAttributes(typeof(ConcurrencyCheckAttribute), true).Any();
            Assert.True(hasConcurrencyCheck, "La propiedad 'RowVersion' debe poseer el atributo [ConcurrencyCheck].");

            // Assert sobre valor por defecto instanciado
            var torneo = new Torneo();
            Assert.NotNull(torneo.RowVersion);
            Assert.Equal(16, torneo.RowVersion.Length);
        }

        [Fact]
        public void Torneo_EFCore_ConfiguradoComoIsConcurrencyToken()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entityType = db.Model.FindEntityType(typeof(Torneo));
            Assert.NotNull(entityType);

            var property = entityType.FindProperty(nameof(Torneo.RowVersion));
            Assert.NotNull(property);
            Assert.True(property.IsConcurrencyToken, "RowVersion debe ser un Concurrency Token en el modelo de EF Core.");
        }

        [Fact]
        public async Task Torneo_ActualizacionEnDbContext_RegeneraRowVersionAutomaticamente()
        {
            var torneoId = Guid.NewGuid();
            byte[] rowVersionInicial;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Concurrencia {Guid.NewGuid():N}".Substring(0, 25),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8,
                    EsPago = false,
                    CostoInscripcion = 0
                };
                db.Torneos.Add(torneo);
                await db.SaveChangesAsync();
                rowVersionInicial = (byte[])torneo.RowVersion.Clone();
            }

            byte[] rowVersionModificado;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var torneo = await db.Torneos.FindAsync(torneoId);
                Assert.NotNull(torneo);

                torneo.Nombre = "Nombre Actualizado Para Concurrencia";
                await db.SaveChangesAsync();
                rowVersionModificado = (byte[])torneo.RowVersion.Clone();
            }

            Assert.False(rowVersionInicial.SequenceEqual(rowVersionModificado),
                "Cada actualización de la entidad Torneo debe regenerar un nuevo RowVersion en SaveChanges.");
        }

        #endregion

        #region Criterio 2: Captura de DbUpdateConcurrencyException en ExceptionHandlingMiddleware (HTTP 409 Conflict)

        [Fact]
        public async Task ExceptionHandlingMiddleware_CapturaDbUpdateConcurrencyException_Retorna409ConflictConMensajeDeReintento()
        {
            // Arrange
            var middleware = new ExceptionHandlingMiddleware(next: (innerContext) =>
            {
                throw new DbUpdateConcurrencyException("Simulación de colisión simultánea en el cupo del torneo");
            });

            var context = new DefaultHttpContext();
            var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            // Act
            await middleware.Invoke(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode); // 409 Conflict

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            var bodyText = await new StreamReader(responseBodyStream).ReadToEndAsync();
            var jsonDoc = JsonDocument.Parse(bodyText);

            Assert.True(jsonDoc.RootElement.TryGetProperty("error", out var errorProp),
                "La respuesta de error debe contener la propiedad 'error' en formato JSON.");

            var errorMsg = errorProp.GetString();
            Assert.NotNull(errorMsg);
            Assert.Contains("El registro fue modificado simultáneamente por otro usuario", errorMsg);
            Assert.Contains("reintente", errorMsg, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Torneo_SimulacionConcurrenciaDosContextos_LanzaDbUpdateConcurrencyException()
        {
            var torneoId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Race {Guid.NewGuid():N}".Substring(0, 20),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8
                });
                await db.SaveChangesAsync();
            }

            // Dos contextos independientes leen el mismo Torneo al mismo tiempo
            using var scope1 = _factory.Services.CreateScope();
            using var scope2 = _factory.Services.CreateScope();

            var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
            var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo1 = await db1.Torneos.FindAsync(torneoId);
            var torneo2 = await db2.Torneos.FindAsync(torneoId);

            Assert.NotNull(torneo1);
            Assert.NotNull(torneo2);

            // Contexto 1 modifica y guarda primero
            torneo1.Nombre = "Modificado por Hilo 1";
            await db1.SaveChangesAsync();

            // Contexto 2 intenta modificar con su RowVersion obsoleto
            torneo2.Nombre = "Modificado por Hilo 2 (Obsoleto)";

            // El segundo contexto debe detectar el conflicto y lanzar DbUpdateConcurrencyException
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
        }

        [Fact]
        public async Task AprobarInscripcion_CuandoOcurreConflictoDeConcurrencia_Retorna409Conflict()
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
                    Nombre = $"Torneo Cupo Limitado {Guid.NewGuid():N}".Substring(0, 25),
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 4,
                    EsPago = false,
                    CostoInscripcion = 0,
                    Estado = EstadoTorneo.Abierto
                };

                var equipo = new Equipo
                {
                    Id = equipoId,
                    Nombre = $"Equipo Concurrente {Guid.NewGuid():N}".Substring(0, 20),
                    TorneoId = torneoId,
                    EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                    PagoValidado = true
                };

                db.Torneos.Add(torneo);
                db.Equipos.Add(equipo);
                await db.SaveChangesAsync();
            }

            // Act: Primera aprobación exitosa
            var res1 = await _client.PatchAsync($"/api/torneos/{torneoId}/equipos/{equipoId}/aprobar", null);
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

            // Segunda aprobación sobre el mismo equipo ya dictaminado -> 409 Conflict (EstadoEquipoInvalidoException)
            var res2 = await _client.PatchAsync($"/api/torneos/{torneoId}/equipos/{equipoId}/aprobar", null);
            Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
        }

        #endregion
    }
}

