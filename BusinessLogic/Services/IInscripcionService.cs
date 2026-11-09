using System;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IInscripcionService
    {
        Task<AprobarInscripcionResponseDTO> AprobarInscripcionAsync(Guid torneoId, Guid equipoId);
        Task<RechazarInscripcionResponseDTO> RechazarInscripcionAsync(Guid torneoId, Guid equipoId, RechazarInscripcionDTO dto);
        Task<EquipoResponseDTO> CambiarEstadoEquipoAsync(Guid equipoId, string nuevoEstadoStr, string? motivoRechazo = null);
        Task<ValidarPagoResponseDTO> ValidarPagoAsync(Guid torneoId, Guid equipoId, ValidarPagoDTO? dto = null);
        Task<ValidarPagoResponseDTO> ValidarPagoEquipoAsync(Guid equipoId, ValidarPagoDTO? dto = null);
    }
}
