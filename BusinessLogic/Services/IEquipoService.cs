using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IEquipoService
    {
        Task<IEnumerable<EquipoResponseDTO>> GetByTorneoIdAsync(Guid torneoId);
        Task<IEnumerable<EquipoPublicResponseDTO>> GetByTorneoIdPublicAsync(Guid torneoId);
        Task<IEnumerable<EquipoPublicResponseDTO>> GetAllPublicAsync();
        Task<EquipoResponseDTO> GetByIdAsync(Guid id);
        Task<EquipoPublicResponseDTO> GetByIdPublicAsync(Guid id);
        Task<EquipoResponseDTO> GetByIdDetalladoAsync(Guid id, Guid? usuarioId, string? rol);
        Task<EquipoResponseDTO> CreateAsync(EquipoCreateDTO dto);
        Task<EquipoResponseDTO> CreateEquipoAsync(Guid torneoId, EquipoCreateDTO dto, Guid? solicitanteId = null, string? rolSolicitante = null);
        Task<EquipoResponseDTO> CreateEquipoAsync(EquipoCreateDTO dto);
        Task<EquipoResponseDTO> CambiarEstadoAsync(Guid id, string nuevoEstado);
        Task<EquipoEstadisticasDTO> GetEstadisticasAsync(Guid id);
    }
}
