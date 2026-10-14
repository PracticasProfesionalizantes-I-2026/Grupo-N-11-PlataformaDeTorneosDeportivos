using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IUsuarioService
    {
        Task<UsuarioResponseDTO> RegisterUsuarioAsync(UsuarioRegistroDTO dto);
    }
}
