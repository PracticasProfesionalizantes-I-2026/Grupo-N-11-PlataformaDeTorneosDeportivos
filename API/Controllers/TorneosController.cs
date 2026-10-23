using System;
using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TorneosController : ControllerBase
    {
        private readonly ITorneoService _torneoService;
        private readonly IFixtureService _fixtureService;

        public TorneosController(ITorneoService torneoService, IFixtureService fixtureService)
        {
            _torneoService = torneoService;
            _fixtureService = fixtureService;
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
        [Authorize(Roles = "Organizador,Administrador")]
        public async Task<IActionResult> CreateTorneo([FromBody] TorneoCreateDTO dto)
        {
            var torneo = await _torneoService.CreateTorneoAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = torneo.Id }, torneo);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Organizador,Administrador")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TorneoUpdateDTO dto)
        {
            var torneo = await _torneoService.UpdateAsync(id, dto);
            return Ok(torneo);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Organizador,Administrador")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _torneoService.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/generar-fixture")]
        [Authorize(Roles = "Organizador,Administrador")]
        public async Task<IActionResult> GenerarFixture(Guid id, [FromBody] GenerarFixtureRequestDTO dto)
        {
            var response = await _fixtureService.GenerarFixtureAsync(id, dto);
            return Ok(response);
        }

        [HttpGet("{id}/fixture")]
        public async Task<IActionResult> GetFixture(Guid id)
        {
            var fixture = await _fixtureService.GetFixtureAsync(id);
            return Ok(fixture);
        }

        [HttpGet("{id}/posiciones")]
        public async Task<IActionResult> GetPosiciones(Guid id)
        {
            var posiciones = await _torneoService.GetPosicionesAsync(id);
            return Ok(posiciones);
        }

        [HttpGet("{id}/llaves")]
        public async Task<IActionResult> GetLlaves(Guid id)
        {
            var llaves = await _torneoService.GetLlavesAsync(id);
            return Ok(llaves);
        }
    }
}
