using System;
using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace API.Controllers
{
    [ApiController]
    [Route("api/partidos/{partidoId:guid}/incidencias")]
    public class IncidenciasController : ControllerBase
    {
        private readonly IIncidenciaService _incidenciaService;

        public IncidenciasController(IIncidenciaService incidenciaService)
        {
            _incidenciaService = incidenciaService;
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarIncidencia(Guid partidoId, [FromBody] IncidenciaCreateDTO dto)
        {
            var response = await _incidenciaService.RegistrarIncidenciaAsync(partidoId, dto);
            return StatusCode(201, response);
        }
    }
}
