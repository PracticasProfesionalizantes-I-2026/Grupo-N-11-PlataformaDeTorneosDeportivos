using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDTO> LoginAsync(LoginRequestDTO dto);
        Task<RecuperarPasswordResponseDTO> RecoverPasswordAsync(RecuperarPasswordRequestDTO dto);
        Task<ResetPasswordResponseDTO> ResetPasswordAsync(ResetPasswordRequestDTO dto);
    }
}
