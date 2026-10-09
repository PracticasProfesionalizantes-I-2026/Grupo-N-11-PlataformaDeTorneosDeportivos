using System;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IUsuarioService
    {
        Task<UsuarioResponseDTO> RegistrarAsync(UsuarioRegistroDTO dto);
        Task<UsuarioResponseDTO> GetByIdAsync(Guid id);
    }
}
