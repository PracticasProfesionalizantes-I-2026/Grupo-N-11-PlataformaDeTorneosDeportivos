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
            if (torneoId == Guid.Empty)
            {
                throw new ValidationException("El identificador del torneo no puede ser vacío.");
            }

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
            if (bannerId == Guid.Empty)
            {
                throw new ValidationException("El identificador del banner publicitario no puede ser vacío.");
            }

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

            if (dto.TorneoId.HasValue && dto.TorneoId.Value == Guid.Empty)
            {
                throw new ValidationException("El identificador del torneo no puede ser vacío.");
            }

            var tituloTrim = dto.Titulo?.Trim() ?? string.Empty;
            var patrocinadorTrim = dto.NombrePatrocinador?.Trim() ?? string.Empty;
            var imagenUrlTrim = dto.ImagenUrl?.Trim() ?? string.Empty;
            var urlDestinoTrim = dto.UrlDestino?.Trim() ?? string.Empty;
            var ubicacionTrim = dto.Ubicacion?.Trim() ?? "Principal";

            if (string.IsNullOrWhiteSpace(tituloTrim) || tituloTrim.Length < 3 || tituloTrim.Length > 100 || tituloTrim.Contains('<') || tituloTrim.Contains('>'))
            {
                throw new ValidationException("El título del banner debe tener entre 3 y 100 caracteres.");
            }

            if (string.IsNullOrWhiteSpace(patrocinadorTrim) || patrocinadorTrim.Length < 2 || patrocinadorTrim.Length > 100 || patrocinadorTrim.Contains('<') || patrocinadorTrim.Contains('>'))
            {
                throw new ValidationException("El nombre del patrocinador debe tener entre 2 y 100 caracteres.");
            }

            if (string.IsNullOrWhiteSpace(imagenUrlTrim) || imagenUrlTrim.Length > 500 || imagenUrlTrim.Contains('<') || imagenUrlTrim.Contains('>') ||
                !Uri.TryCreate(imagenUrlTrim, UriKind.Absolute, out var imgUri) || (imgUri.Scheme != Uri.UriSchemeHttp && imgUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ValidationException("La URL de la imagen es obligatoria, debe tener formato válido (http/https) y no puede superar los 500 caracteres.");
            }

            if (string.IsNullOrWhiteSpace(urlDestinoTrim) || urlDestinoTrim.Length > 500 || urlDestinoTrim.Contains('<') || urlDestinoTrim.Contains('>') ||
                !Uri.TryCreate(urlDestinoTrim, UriKind.Absolute, out var destUri) || (destUri.Scheme != Uri.UriSchemeHttp && destUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ValidationException("La URL de destino es obligatoria, debe tener formato válido (http/https) y no puede superar los 500 caracteres.");
            }

            if (string.IsNullOrWhiteSpace(ubicacionTrim) || ubicacionTrim.Length < 2 || ubicacionTrim.Length > 50 || ubicacionTrim.Contains('<') || ubicacionTrim.Contains('>'))
            {
                throw new ValidationException("La ubicación del banner debe tener entre 2 y 50 caracteres válidos.");
            }

            var entity = new Publicidad
            {
                Id = Guid.NewGuid(),
                TorneoId = dto.TorneoId,
                Titulo = tituloTrim,
                NombrePatrocinador = patrocinadorTrim,
                ImagenUrl = imagenUrlTrim,
                UrlDestino = urlDestinoTrim,
                Ubicacion = ubicacionTrim,
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
    }
}
