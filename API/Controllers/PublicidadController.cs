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
    public class PublicidadController : ControllerBase
    {
        private readonly IPublicidadService _publicidadService;

        public PublicidadController(IPublicidadService publicidadService)
        {
            _publicidadService = publicidadService;
        }

        [HttpGet("torneos/{id}/publicidad")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicidadByTorneo(Guid id)
        {
            var banners = await _publicidadService.GetPublicidadByTorneoAsync(id);
            return Ok(banners);
        }

        [HttpPost("publicidad/{bannerId}/click")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterClick(Guid bannerId)
        {
            var response = await _publicidadService.RegisterClickAsync(bannerId);
            return Ok(response);
        }

        [HttpPost("publicidad")]
        [Authorize(Roles = "Organizador,Administrador")]
        public async Task<IActionResult> Create([FromBody] PublicidadCreateDTO dto)
        {
            var banner = await _publicidadService.CreateAsync(dto);
            return StatusCode(201, banner);
        }

        [HttpPost("torneos/{id}/publicidad")]
        [Authorize(Roles = "Organizador,Administrador")]
        public async Task<IActionResult> CreateByTorneo(Guid id, [FromBody] PublicidadCreateDTO dto)
        {
            dto.TorneoId = id;
            var banner = await _publicidadService.CreateAsync(dto);
            return StatusCode(201, banner);
        }
    }
}
