using System;
using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PartidosController : ControllerBase
    {
        private readonly IPartidoService _partidoService;

        public PartidosController(IPartidoService partidoService)
        {
            _partidoService = partidoService;
        }

        [HttpPut("{id:guid}/resultado")]
        public async Task<IActionResult> LoadResultado(Guid id, [FromBody] CargarResultadoDTO dto)
        {
            var result = await _partidoService.LoadResultadoAsync(id, dto);
            return Ok(result);
        }
    }
}
