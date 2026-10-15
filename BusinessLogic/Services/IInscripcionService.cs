using System;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IInscripcionService
    {
        Task<AprobarInscripcionResponseDTO> AprobarInscripcionAsync(Guid torneoId, Guid equipoId);
        Task<RechazarInscripcionResponseDTO> RechazarInscripcionAsync(Guid torneoId, Guid equipoId, RechazarInscripcionDTO dto);
    }
}
