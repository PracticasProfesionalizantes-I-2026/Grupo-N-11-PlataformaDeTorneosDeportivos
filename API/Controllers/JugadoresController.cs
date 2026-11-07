using System;
using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace API.Controllers
{
    [ApiController]
    [Route("api")]
    public class JugadoresController : ControllerBase
    {
        private readonly IJugadorService _jugadorService;

        public JugadoresController(IJugadorService jugadorService)
        {
            _jugadorService = jugadorService;
        }

        [HttpGet("equipos/{equipoId}/jugadores")]
        public async Task<IActionResult> GetByEquipoId(Guid equipoId)
        {
            var jugadores = await _jugadorService.GetByEquipoIdAsync(equipoId);
            return Ok(jugadores);
        }

        [HttpPost("equipos/{equipoId}/jugadores")]
        [Authorize(Roles = "Capitan,Administrador")]
        public async Task<IActionResult> Create(Guid equipoId, [FromBody] JugadorCreateDTO dto)
        {
            dto.EquipoId = equipoId;
            var jugador = await _jugadorService.CreateAsync(dto);
            return Ok(jugador);
        }

        [HttpDelete("jugadores/{id}")]
        [Authorize(Roles = "Capitan,Administrador")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _jugadorService.DeleteAsync(id);
            return NoContent();
        }

        [HttpGet("jugadores/{id}/estadisticas")]
        public async Task<IActionResult> GetEstadisticas(Guid id)
        {
            var estadisticas = await _jugadorService.GetEstadisticasAsync(id);
            return Ok(estadisticas);
        }
    }
}
