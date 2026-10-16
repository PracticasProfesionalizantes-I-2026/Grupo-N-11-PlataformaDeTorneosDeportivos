using System;
using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VeedoresController : ControllerBase
    {
        private readonly IVeedorService _veedorService;

        public VeedoresController(IVeedorService veedorService)
        {
            _veedorService = veedorService;
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _veedorService.GetByIdAsync(id);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _veedorService.GetAllAsync();
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateVeedor([FromBody] VeedorCreateDTO dto)
        {
            var result = await _veedorService.CreateVeedorAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateVeedor(Guid id, [FromBody] VeedorUpdateDTO dto)
        {
            var result = await _veedorService.UpdateVeedorAsync(id, dto);
            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeactivateVeedor(Guid id)
        {
            await _veedorService.DeactivateVeedorAsync(id);
            return NoContent();
        }

        [HttpGet("{id:guid}/partidos")]
        public async Task<IActionResult> GetPartidos(
            Guid id,
            [FromQuery] DateTime? fecha,
            [FromQuery] Guid? torneoId)
        {
            Guid? currentUserId = null;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var parsedId))
            {
                currentUserId = parsedId;
            }
            var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            var result = await _veedorService.GetPartidosByVeedorAsync(id, fecha, torneoId, currentUserId, roleClaim);
            return Ok(result);
        }
    }
}
