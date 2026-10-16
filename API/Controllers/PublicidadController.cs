using System;
using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Mvc;

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

        [HttpGet("torneos/{id:guid}/publicidad")]
        public async Task<IActionResult> GetPublicidadByTorneo(Guid id)
        {
            var banners = await _publicidadService.GetPublicidadByTorneoAsync(id);
            return Ok(banners);
        }

        [HttpPost("publicidad/{bannerId:guid}/click")]
        public async Task<IActionResult> RegisterClick(Guid bannerId)
        {
            var response = await _publicidadService.RegisterClickAsync(bannerId);
            return Ok(response);
        }
    }
}
