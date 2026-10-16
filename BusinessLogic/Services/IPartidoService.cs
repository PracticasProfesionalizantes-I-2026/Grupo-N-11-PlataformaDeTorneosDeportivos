using System;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IPartidoService
    {
        Task<PartidoResultadoResponseDTO> LoadResultadoAsync(Guid id, CargarResultadoDTO dto);
    }
}
