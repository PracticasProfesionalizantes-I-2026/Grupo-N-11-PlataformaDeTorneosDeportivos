using System;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IIncidenciaService
    {
        Task<IncidenciaResponseDTO> RegistrarIncidenciaAsync(Guid partidoId, IncidenciaCreateDTO dto);
    }
}
