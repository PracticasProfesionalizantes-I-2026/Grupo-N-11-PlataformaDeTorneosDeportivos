using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IPublicidadService
    {
        Task<IEnumerable<PublicidadResponseDTO>> GetPublicidadByTorneoAsync(Guid torneoId);
        Task<PublicidadClickResponseDTO> RegisterClickAsync(Guid bannerId);
        Task<PublicidadResponseDTO> CreateAsync(PublicidadCreateDTO dto);
    }
}
