using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IJugadorService
    {
        Task<IEnumerable<JugadorResponseDTO>> GetByEquipoIdAsync(Guid equipoId);
        Task<JugadorResponseDTO> CreateAsync(JugadorCreateDTO dto);
        Task DeleteAsync(Guid id);
    }
}
