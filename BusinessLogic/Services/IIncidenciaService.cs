using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IIncidenciaService
    {
        Task<IncidenciaResponseDTO> RegistrarIncidenciaAsync(Guid partidoId, IncidenciaCreateDTO dto);
        Task<IEnumerable<IncidenciaResponseDTO>> GetByPartidoIdAsync(Guid partidoId);
    }
}
