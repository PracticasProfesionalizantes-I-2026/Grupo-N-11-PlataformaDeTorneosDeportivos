using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface ITorneoService
    {
        Task<IEnumerable<TorneoResponseDTO>> GetAllAsync();
        Task<TorneoResponseDTO> GetByIdAsync(Guid id);
        Task<TorneoResponseDTO> CreateAsync(TorneoCreateDTO dto);
        Task<TorneoResponseDTO> UpdateAsync(Guid id, TorneoUpdateDTO dto);
        Task DeleteAsync(Guid id);
    }
}
