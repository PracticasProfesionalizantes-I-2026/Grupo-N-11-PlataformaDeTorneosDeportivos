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

        [HttpGet("equipos/{equipoId:guid}/jugadores")]
        public async Task<IActionResult> GetByEquipoId(Guid equipoId)
        {
            var jugadores = await _jugadorService.GetByEquipoIdAsync(equipoId);
            return Ok(jugadores);
        }

        [HttpPost("equipos/{equipoId:guid}/jugadores")]
        [Authorize(Roles = "Capitan,Administrador")]
        public async Task<IActionResult> Create(Guid equipoId, [FromBody] JugadorCreateDTO dto)
        {
            dto.EquipoId = equipoId;
            var jugador = await _jugadorService.CreateAsync(dto);
            return Ok(jugador);
        }

        [HttpDelete("jugadores/{id:guid}")]
        [Authorize(Roles = "Capitan,Administrador")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _jugadorService.DeleteAsync(id);
            return NoContent();
        }

        [HttpGet("jugadores/{id:guid}/estadisticas")]
        public async Task<IActionResult> GetEstadisticas(Guid id)
        {
            var estadisticas = await _jugadorService.GetEstadisticasAsync(id);
            return Ok(estadisticas);
        }
    }
}
