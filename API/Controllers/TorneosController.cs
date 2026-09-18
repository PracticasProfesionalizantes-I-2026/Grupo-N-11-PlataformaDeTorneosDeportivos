using System;
using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TorneosController : ControllerBase
    {
        private readonly ITorneoService _torneoService;

        public TorneosController(ITorneoService torneoService)
        {
            _torneoService = torneoService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var torneos = await _torneoService.GetAllAsync();
            return Ok(torneos);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var torneo = await _torneoService.GetByIdAsync(id);
            return Ok(torneo);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TorneoCreateDTO dto)
        {
            var torneo = await _torneoService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = torneo.Id }, torneo);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TorneoUpdateDTO dto)
        {
            var torneo = await _torneoService.UpdateAsync(id, dto);
            return Ok(torneo);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _torneoService.DeleteAsync(id);
            return NoContent();
        }
    }
}
