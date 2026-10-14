using System.Threading.Tasks;
using Shared.DTOs;

namespace BusinessLogic.Services
{
    public interface IRecuperacionPasswordService
    {
        Task<bool> SolicitarRecuperacionAsync(RecuperarPasswordDTO dto);
        Task<bool> ResetearPasswordAsync(ResetPasswordDTO dto);
    }
}
