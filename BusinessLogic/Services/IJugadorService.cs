using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IJugadorService
    {
        Task<IEnumerable<JugadorResponseDTO>> GetByEquipoIdAsync(Guid equipoId);
        Task<IEnumerable<JugadorPublicResponseDTO>> GetByEquipoIdPublicAsync(Guid equipoId);
        Task<IEnumerable<JugadorResponseDTO>> GetByEquipoIdDetalladoAsync(Guid equipoId, Guid? usuarioId, string? rol);
        Task<JugadorResponseDTO> CreateAsync(JugadorCreateDTO dto, Guid? solicitanteId = null, string? rolSolicitante = null);
        Task DeleteAsync(Guid id, Guid? solicitanteId = null, string? rolSolicitante = null);
        Task<JugadorEstadisticasDTO> GetEstadisticasAsync(Guid id);
    }
}
