using System;
using System.Security.Claims;
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
        private readonly IInscripcionService? _inscripcionService;

        public EquiposController(IEquipoService equipoService, IInscripcionService? inscripcionService = null)
        {
            _equipoService = equipoService;
            _inscripcionService = inscripcionService;
        }

        [HttpGet("torneos/{torneoId}/equipos")]
        public async Task<IActionResult> GetByTorneoId(Guid torneoId)
        {
            var isAuthorized = User.Identity?.IsAuthenticated == true &&
                (User.IsInRole("Organizador") || User.IsInRole("Administrador"));

            if (isAuthorized)
            {
                var equipos = await _equipoService.GetByTorneoIdAsync(torneoId);
                return Ok(equipos);
            }
            else
            {
                var equiposPublicos = await _equipoService.GetByTorneoIdPublicAsync(torneoId);
                return Ok(equiposPublicos);
            }
        }

        [HttpGet("equipos")]
        public async Task<IActionResult> GetAll()
        {
            var equiposPublicos = await _equipoService.GetAllPublicAsync();
            return Ok(equiposPublicos);
        }

        [HttpGet("equipos/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var (usuarioId, rol) = GetUserContext();
            var equipo = await _equipoService.GetByIdAsync(id);

            var isAuthorized = (rol == "Organizador" || rol == "Administrador") ||
                               (rol == "Capitan" && usuarioId.HasValue && equipo.CapitanId.HasValue && equipo.CapitanId.Value == usuarioId.Value);

            if (isAuthorized)
            {
                return Ok(equipo);
            }

            var equipoPublico = await _equipoService.GetByIdPublicAsync(id);
            return Ok(equipoPublico);
        }

        [HttpGet("equipos/{id}/nomina-completa")]
        [Authorize]
        public async Task<IActionResult> GetNominaCompleta(Guid id)
        {
            var (usuarioId, rol) = GetUserContext();
            var equipo = await _equipoService.GetByIdDetalladoAsync(id, usuarioId, rol);
            return Ok(equipo);
        }

        [HttpGet("equipos/{id}/admin")]
        [Authorize]
        public async Task<IActionResult> GetAdmin(Guid id)
        {
            var (usuarioId, rol) = GetUserContext();
            var equipo = await _equipoService.GetByIdDetalladoAsync(id, usuarioId, rol);
            return Ok(equipo);
        }

        [HttpPost("torneos/{torneoId}/equipos")]
        [Authorize(Roles = "Capitan,Organizador,Administrador")]
        public async Task<IActionResult> CreateEquipo(Guid torneoId, [FromBody] EquipoCreateDTO dto)
        {
            dto.TorneoId = torneoId;
            var (usuarioId, rol) = GetUserContext();
            var equipo = await _equipoService.CreateEquipoAsync(torneoId, dto, usuarioId, rol);
            return CreatedAtAction(nameof(GetById), new { id = equipo.Id }, equipo);
        }

        [HttpPut("equipos/{id}/estado")]
        [Authorize(Roles = "Organizador,Administrador")]
        public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] string estado)
        {
            var equipo = await _equipoService.CambiarEstadoAsync(id, estado);
            return Ok(equipo);
        }

        [HttpGet("equipos/{id}/estadisticas")]
        public async Task<IActionResult> GetEstadisticas(Guid id)
        {
            var estadisticas = await _equipoService.GetEstadisticasAsync(id);
            return Ok(estadisticas);
        }

        [HttpPatch("equipos/{id}/validar-pago")]
        [Authorize(Roles = "Organizador,Administrador")]
        public async Task<IActionResult> ValidarPago(Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ValidarPagoDTO? dto = null)
        {
            if (_inscripcionService != null)
            {
                var result = await _inscripcionService.ValidarPagoEquipoAsync(id, dto);
                return Ok(result);
            }
            return NotFound();
        }

        private (Guid? usuarioId, string? rol) GetUserContext()
        {
            if (User.Identity?.IsAuthenticated != true)
                return (null, null);

            Guid? usuarioId = null;
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(sub, out var parsedGuid))
            {
                usuarioId = parsedGuid;
            }

            string? rol = null;
            if (User.IsInRole("Administrador"))
                rol = "Administrador";
            else if (User.IsInRole("Organizador"))
                rol = "Organizador";
            else if (User.IsInRole("Capitan"))
                rol = "Capitan";
            else if (User.IsInRole("Veedor"))
                rol = "Veedor";
            else
                rol = User.FindFirst(ClaimTypes.Role)?.Value;

            return (usuarioId, rol);
        }
    }
}
