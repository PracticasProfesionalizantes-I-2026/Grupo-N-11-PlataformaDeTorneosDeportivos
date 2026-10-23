using System;
using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace API.Controllers
{
    [ApiController]
    [Route("api/torneos/{torneoId}/equipos/{equipoId}")]
    [Authorize(Roles = "Organizador,Administrador")]
    public class InscripcionesController : ControllerBase
    {
        private readonly IInscripcionService _inscripcionService;

        public InscripcionesController(IInscripcionService inscripcionService)
        {
            _inscripcionService = inscripcionService;
        }

        [HttpPatch("aprobar")]
        public async Task<IActionResult> AprobarInscripcion(Guid torneoId, Guid equipoId)
        {
            var result = await _inscripcionService.AprobarInscripcionAsync(torneoId, equipoId);
            return Ok(result);
        }

        [HttpPatch("rechazar")]
        public async Task<IActionResult> RechazarInscripcion(Guid torneoId, Guid equipoId, [FromBody] RechazarInscripcionDTO dto)
        {
            var result = await _inscripcionService.RechazarInscripcionAsync(torneoId, equipoId, dto);
            return Ok(result);
        }
    }
}
