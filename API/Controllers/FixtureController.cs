using System;
using System.Threading.Tasks;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace API.Controllers
{
    [ApiController]
    [Route("api/torneos/{torneoId:guid}/fixture")]
    public class FixtureController : ControllerBase
    {
        private readonly IFixtureService _fixtureService;

        public FixtureController(IFixtureService fixtureService)
        {
            _fixtureService = fixtureService;
        }

        [HttpPost]
        public async Task<IActionResult> GenerarFixture(Guid torneoId, [FromBody] FixtureParametrosDTO dto)
        {
            var response = await _fixtureService.GenerarFixtureAsync(torneoId, dto);
            return Created($"/api/torneos/{torneoId}/fixture", response);
        }
    }
}
