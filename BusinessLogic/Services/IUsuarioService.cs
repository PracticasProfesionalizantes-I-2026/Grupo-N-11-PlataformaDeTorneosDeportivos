using System;
using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IUsuarioService
    {
        Task<UsuarioResponseDTO> RegisterUsuarioAsync(UsuarioRegistroDTO dto);
        Task<UsuarioResponseDTO> CreateUsuarioAdminAsync(UsuarioRegistroDTO dto, string? rolSolicitante = null);
        Task<UsuarioResponseDTO> CambiarRolAsync(Guid id, CambiarRolDTO dto, string? rolSolicitante = null);
        Task<UsuarioResponseDTO> GetByIdAsync(Guid id);
    }
}
