using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Services;
using DataAccess.Entities;
using DataAccess.Repositories;
using Moq;
using Shared.DTOs;
using Shared.Exceptions;
using Xunit;

namespace BusinessLogic.Tests
{
    public class PublicidadServiceTests
    {
        private readonly Mock<IPublicidadRepository> _publicidadRepositoryMock;
        private readonly Mock<ITorneoRepository> _torneoRepositoryMock;
        private readonly PublicidadService _publicidadService;

        public PublicidadServiceTests()
        {
            _publicidadRepositoryMock = new Mock<IPublicidadRepository>();
            _torneoRepositoryMock = new Mock<ITorneoRepository>();
            _publicidadService = new PublicidadService(
                _publicidadRepositoryMock.Object,
                _torneoRepositoryMock.Object);
        }

        [Fact]
        public async Task GetPublicidadByTorneoAsync_ReturnsBannersAndLogsImpression()
        {
            // Arrange (CU-15 Flujo Principal - Visualizar)
            var torneoId = Guid.NewGuid();
            var banners = new List<Publicidad>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TorneoId = torneoId,
                    Titulo = "Bebida Energética X",
                    NombrePatrocinador = "EnergyCorp",
                    ImagenUrl = "https://example.com/energy.png",
                    UrlDestino = "https://energycorp.com",
                    Ubicacion = "Superior",
                    Activo = true,
                    Impresiones = 10,
                    Clicks = 2
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TorneoId = torneoId,
                    Titulo = "Ropa Deportiva Pro",
                    NombrePatrocinador = "ProSport",
                    ImagenUrl = "https://example.com/prosport.png",
                    UrlDestino = "https://prosport.com",
                    Ubicacion = "Lateral",
                    Activo = true,
                    Impresiones = 25,
                    Clicks = 5
                }
            };

            _publicidadRepositoryMock.Setup(r => r.GetByTorneoIdAsync(torneoId))
                .ReturnsAsync(banners);

            // Act
            var result = (await _publicidadService.GetPublicidadByTorneoAsync(torneoId)).ToList();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Bebida Energética X", result[0].Titulo);
            Assert.Equal("Ropa Deportiva Pro", result[1].Titulo);
            Assert.Equal(11, result[0].Impresiones);
            Assert.Equal(26, result[1].Impresiones);

            _publicidadRepositoryMock.Verify(r => r.IncrementImpressionsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Count() == 2)), Times.Once);
        }

        [Fact]
        public async Task RegisterClickAsync_LogsClickMetric()
        {
            // Arrange (CU-15 Flujo Principal - Click)
            var bannerId = Guid.NewGuid();
            var banner = new Publicidad
            {
                Id = bannerId,
                Titulo = "Calzado Deportivo Ultra",
                NombrePatrocinador = "SpeedShoes",
                ImagenUrl = "https://example.com/banner.png",
                UrlDestino = "https://speedshoes.com/promo",
                Clicks = 15,
                Impresiones = 200,
                Activo = true
            };

            _publicidadRepositoryMock.Setup(r => r.GetByIdAsync(bannerId))
                .ReturnsAsync(banner);

            _publicidadRepositoryMock.Setup(r => r.IncrementClickAsync(bannerId))
                .ReturnsAsync(16);

            // Act
            var result = await _publicidadService.RegisterClickAsync(bannerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(bannerId, result.BannerId);
            Assert.Equal("https://speedshoes.com/promo", result.UrlDestino);
            Assert.Equal(16, result.TotalClicks);

            _publicidadRepositoryMock.Verify(r => r.IncrementClickAsync(bannerId), Times.Once);
        }

        [Fact]
        public async Task GetPublicidadByTorneoAsync_WhenEmpty_ReturnsEmptyList()
        {
            // Arrange (CU-15 Flujo Alternativo 3a)
            var torneoId = Guid.NewGuid();
            _publicidadRepositoryMock.Setup(r => r.GetByTorneoIdAsync(torneoId))
                .ReturnsAsync(new List<Publicidad>());

            // Act
            var result = await _publicidadService.GetPublicidadByTorneoAsync(torneoId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);

            _publicidadRepositoryMock.Verify(r => r.IncrementImpressionsAsync(It.IsAny<IEnumerable<Guid>>()), Times.Never);
        }

        [Fact]
        public async Task RegisterClickAsync_WhenNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var bannerId = Guid.NewGuid();
            _publicidadRepositoryMock.Setup(r => r.GetByIdAsync(bannerId))
                .ReturnsAsync((Publicidad?)null);

            // Act & Assert
            await Assert.ThrowsAsync<PublicidadNotFoundException>(() =>
                _publicidadService.RegisterClickAsync(bannerId));

            _publicidadRepositoryMock.Verify(r => r.IncrementClickAsync(It.IsAny<Guid>()), Times.Never);
        }
    }
}
