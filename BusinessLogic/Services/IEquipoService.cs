using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IEquipoService
    {
        Task<IEnumerable<EquipoResponseDTO>> GetByTorneoIdAsync(Guid torneoId);
        Task<EquipoResponseDTO> GetByIdAsync(Guid id);
        Task<EquipoResponseDTO> CreateAsync(EquipoCreateDTO dto);
        Task<EquipoResponseDTO> CambiarEstadoAsync(Guid id, string nuevoEstado);
    }
}
