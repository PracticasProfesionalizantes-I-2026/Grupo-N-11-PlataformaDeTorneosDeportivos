using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IVeedorService
    {
        Task<VeedorResponseDTO> CreateVeedorAsync(VeedorCreateDTO dto);
        Task<VeedorResponseDTO> UpdateVeedorAsync(Guid id, VeedorUpdateDTO dto);
        Task DeactivateVeedorAsync(Guid id);
        Task<VeedorResponseDTO> GetByIdAsync(Guid id);
        Task<IEnumerable<VeedorResponseDTO>> GetAllAsync();
    }
}
