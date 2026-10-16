using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class PublicidadService : IPublicidadService
    {
        private readonly IPublicidadRepository _publicidadRepository;
        private readonly ITorneoRepository? _torneoRepository;

        public PublicidadService(
            IPublicidadRepository publicidadRepository,
            ITorneoRepository? torneoRepository = null)
        {
            _publicidadRepository = publicidadRepository;
            _torneoRepository = torneoRepository;
        }

        public async Task<IEnumerable<PublicidadResponseDTO>> GetPublicidadByTorneoAsync(Guid torneoId)
        {
            var banners = (await _publicidadRepository.GetByTorneoIdAsync(torneoId)).ToList();

            if (banners.Count == 0)
            {
                return Enumerable.Empty<PublicidadResponseDTO>();
            }

            // Registra asíncronamente una "Impresión" (visualización) en la base de datos
            await _publicidadRepository.IncrementImpressionsAsync(banners.Select(b => b.Id));

            // Actualizar el valor en memoria de los objetos para la respuesta inmediata
            foreach (var b in banners)
            {
                b.Impresiones++;
            }

            return banners.Select(MapToResponseDTO).ToList();
        }

        public async Task<PublicidadClickResponseDTO> RegisterClickAsync(Guid bannerId)
        {
            var banner = await _publicidadRepository.GetByIdAsync(bannerId);
            if (banner == null)
            {
                throw new PublicidadNotFoundException($"No se encontró el banner publicitario con ID {bannerId}");
            }

            var totalClicks = await _publicidadRepository.IncrementClickAsync(bannerId);

            return new PublicidadClickResponseDTO
            {
                BannerId = banner.Id,
                UrlDestino = banner.UrlDestino,
                TotalClicks = totalClicks > 0 ? totalClicks : banner.Clicks + 1,
                Mensaje = "Click registrado exitosamente."
            };
        }

        public async Task<PublicidadResponseDTO> CreateAsync(PublicidadCreateDTO dto)
        {
            if (dto == null)
            {
                throw new ValidationException("Los datos de la publicidad son obligatorios.");
            }

            var entity = new Publicidad
            {
                Id = Guid.NewGuid(),
                TorneoId = dto.TorneoId,
                Titulo = dto.Titulo,
                NombrePatrocinador = dto.NombrePatrocinador,
                ImagenUrl = dto.ImagenUrl,
                UrlDestino = dto.UrlDestino,
                Ubicacion = dto.Ubicacion,
                Activo = dto.Activo,
                Impresiones = 0,
                Clicks = 0,
                FechaCreacion = DateTime.UtcNow
            };

            await _publicidadRepository.AddAsync(entity);
            return MapToResponseDTO(entity);
        }

        private static PublicidadResponseDTO MapToResponseDTO(Publicidad p)
        {
            return new PublicidadResponseDTO
            {
                Id = p.Id,
                TorneoId = p.TorneoId,
                Titulo = p.Titulo,
                NombrePatrocinador = p.NombrePatrocinador,
                ImagenUrl = p.ImagenUrl,
                UrlDestino = p.UrlDestino,
                Ubicacion = p.Ubicacion,
                Activo = p.Activo,
                Impresiones = p.Impresiones,
                Clicks = p.Clicks
            };
        }

        private static PublicidadClickResponseDTO MapToClickResponseDTO(Publicidad p)
        {
            return new PublicidadClickResponseDTO
            {
                BannerId = p.Id,
                UrlDestino = p.UrlDestino,
                TotalClicks = p.Clicks,
                Mensaje = "Click registrado exitosamente."
            };
        }
    }
}
