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
    public class EquiposController : ControllerBase
    {
        private readonly IEquipoService _equipoService;

        public EquiposController(IEquipoService equipoService)
        {
            _equipoService = equipoService;
        }

        [HttpGet("torneos/{torneoId:guid}/equipos")]
        public async Task<IActionResult> GetByTorneoId(Guid torneoId)
        {
            var equipos = await _equipoService.GetByTorneoIdAsync(torneoId);
            return Ok(equipos);
        }

        [HttpGet("equipos/{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var equipo = await _equipoService.GetByIdAsync(id);
            return Ok(equipo);
        }

        [HttpPost("torneos/{torneoId:guid}/equipos")]
        [Authorize(Roles = "Capitan,Administrador")]
        public async Task<IActionResult> CreateEquipo(Guid torneoId, [FromBody] EquipoCreateDTO dto)
        {
            dto.TorneoId = torneoId;
            var equipo = await _equipoService.CreateEquipoAsync(torneoId, dto);
            return CreatedAtAction(nameof(GetById), new { id = equipo.Id }, equipo);
        }

        [HttpPut("equipos/{id:guid}/estado")]
        [Authorize(Roles = "Organizador,Administrador")]
        public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] string estado)
        {
            var equipo = await _equipoService.CambiarEstadoAsync(id, estado);
            return Ok(equipo);
        }

        [HttpGet("equipos/{id:guid}/estadisticas")]
        public async Task<IActionResult> GetEstadisticas(Guid id)
        {
            var estadisticas = await _equipoService.GetEstadisticasAsync(id);
            return Ok(estadisticas);
        }
    }
}
