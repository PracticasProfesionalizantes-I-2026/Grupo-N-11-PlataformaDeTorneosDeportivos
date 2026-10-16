using System;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IFixtureService
    {
        Task<GenerarFixtureResponseDTO> GenerarFixtureAsync(Guid torneoId, GenerarFixtureRequestDTO dto);
        Task<FixtureResponseDTO> GenerarFixtureAsync(Guid torneoId, FixtureParametrosDTO dto);
        Task<IEnumerable<PartidoResponseDTO>> GetFixtureAsync(Guid torneoId);
    }
}
