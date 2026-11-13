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
    public class PublicidadIntegrationTests : IClassFixture<CustomWebApplicationFactory<API.Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<API.Program> _factory;

        public PublicidadIntegrationTests(CustomWebApplicationFactory<API.Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetPublicidad_Returns200OKAndLogsMetric()
        {
            // Arrange (CU-15 Flujo Principal HTTP - Ver)
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = $"Torneo Publicidad {Guid.NewGuid():N}",
                Disciplina = Disciplina.Futbol,
                Modalidad = Modalidad.Liga,
                CupoMaximo = 8,
                Estado = EstadoTorneo.EnDesarrollo
            };
            db.Torneos.Add(torneo);

            var banner = new Publicidad
            {
                Id = Guid.NewGuid(),
                TorneoId = torneo.Id,
                Titulo = "Sponsor Oficial",
                NombrePatrocinador = "MegaCorp",
                ImagenUrl = "https://example.com/banner.png",
                UrlDestino = "https://megacorp.com",
                Ubicacion = "Superior",
                Activo = true,
                Impresiones = 0,
                Clicks = 0
            };
            db.Publicidades.Add(banner);
            await db.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/torneos/{torneo.Id}/publicidad");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var banners = await response.Content.ReadFromJsonAsync<List<PublicidadResponseDTO>>();
            Assert.NotNull(banners);
            Assert.NotEmpty(banners);
            Assert.Equal("Sponsor Oficial", banners[0].Titulo);
            Assert.Equal(1, banners[0].Impresiones);
        }

        [Fact]
        public async Task PostPublicidadClick_Returns200OK()
        {
            // Arrange (CU-15 Flujo Principal HTTP - Click)
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var banner = new Publicidad
            {
                Id = Guid.NewGuid(),
                Titulo = "Bebida Deportiva",
                NombrePatrocinador = "GatorMax",
                ImagenUrl = "https://example.com/gatormax.png",
                UrlDestino = "https://gatormax.com",
                Ubicacion = "Lateral",
                Activo = true,
                Impresiones = 10,
                Clicks = 3
            };
            db.Publicidades.Add(banner);
            await db.SaveChangesAsync();

            // Act
            var response = await _client.PostAsync($"/api/publicidad/{banner.Id}/click", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var clickResponse = await response.Content.ReadFromJsonAsync<PublicidadClickResponseDTO>();
            Assert.NotNull(clickResponse);
            Assert.Equal(banner.Id, clickResponse.BannerId);
            Assert.Equal("https://gatormax.com", clickResponse.UrlDestino);
            Assert.Equal(4, clickResponse.TotalClicks);
        }

        [Fact]
        public async Task GetPublicidad_WhenNoBanners_Returns200OKAndEmpty()
        {
            // Arrange (CU-15 Flujo 3a HTTP - Sin publicidad)
            var torneoId = Guid.NewGuid();

            // Act
            var response = await _client.GetAsync($"/api/torneos/{torneoId}/publicidad");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var banners = await response.Content.ReadFromJsonAsync<List<PublicidadResponseDTO>>();
            Assert.NotNull(banners);
            Assert.Empty(banners);
        }

        [Fact]
        public async Task PostPublicidadClick_WhenNotFound_Returns404NotFound()
        {
            // Act
            var response = await _client.PostAsync($"/api/publicidad/{Guid.NewGuid()}/click", null);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreatePublicidad_WithValidPayload_Returns201Created()
        {
            var torneoId = Guid.NewGuid();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Torneos.Add(new Torneo
                {
                    Id = torneoId,
                    Nombre = $"Torneo Sponsor {Guid.NewGuid():N}",
                    Disciplina = Disciplina.Futbol,
                    Modalidad = Modalidad.Liga,
                    CupoMaximo = 8,
                    Estado = EstadoTorneo.Abierto
                });
                await db.SaveChangesAsync();
            }

            var dto = new PublicidadCreateDTO
            {
                TorneoId = torneoId,
                Titulo = "Nuevo Sponsor Oficial",
                NombrePatrocinador = "Adidas Pro",
                ImagenUrl = "https://adidas.com/banner.png",
                UrlDestino = "https://adidas.com",
                Ubicacion = "Superior",
                Activo = true
            };

            var response = await _client.PostAsJsonAsync($"/api/torneos/{torneoId}/publicidad", dto);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<PublicidadResponseDTO>();
            Assert.NotNull(created);
            Assert.Equal("Nuevo Sponsor Oficial", created.Titulo);
            Assert.Equal("Adidas Pro", created.NombrePatrocinador);
            Assert.Equal(torneoId, created.TorneoId);
        }
    }
}
