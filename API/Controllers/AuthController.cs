using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IRecuperacionPasswordService _recuperacionService;

        public AuthController(IRecuperacionPasswordService recuperacionService)
        {
            _recuperacionService = recuperacionService;
        }

        [HttpPost("recuperar-password")]
        public async Task<IActionResult> SolicitarRecuperacion([FromBody] RecuperarPasswordDTO dto)
        {
            await _recuperacionService.SolicitarRecuperacionAsync(dto);
            return Ok(new { mensaje = "Si el correo existe, se enviará un enlace de recuperación." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto)
        {
            await _recuperacionService.ResetearPasswordAsync(dto);
            return Ok(new { mensaje = "Contraseña actualizada exitosamente." });
        }
    }
}
