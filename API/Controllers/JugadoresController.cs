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
    public class JugadoresController : ControllerBase
    {
        private readonly IJugadorService _jugadorService;
        private readonly IEquipoService? _equipoService;

        public JugadoresController(IJugadorService jugadorService, IEquipoService? equipoService = null)
        {
            _jugadorService = jugadorService;
            _equipoService = equipoService;
        }

        [HttpGet("equipos/{equipoId}/jugadores")]
        public async Task<IActionResult> GetByEquipoId(Guid equipoId)
        {
            var (usuarioId, rol) = GetUserContext();

            bool isAuthorized = rol == "Organizador" || rol == "Administrador";
            if (!isAuthorized && _equipoService != null && usuarioId.HasValue && rol == "Capitan")
            {
                try
                {
                    var equipo = await _equipoService.GetByIdAsync(equipoId);
                    if (equipo?.CapitanId.HasValue == true && equipo.CapitanId.Value == usuarioId.Value)
                    {
                        isAuthorized = true;
                    }
                }
                catch
                {
                    // Fallback to public
                }
            }

            if (isAuthorized)
            {
                var jugadores = await _jugadorService.GetByEquipoIdAsync(equipoId);
                return Ok(jugadores);
            }

            var publicJugadores = await _jugadorService.GetByEquipoIdPublicAsync(equipoId);
            return Ok(publicJugadores);
        }

        [HttpGet("equipos/{equipoId}/jugadores/nomina-completa")]
        [Authorize]
        public async Task<IActionResult> GetNominaCompleta(Guid equipoId)
        {
            var (usuarioId, rol) = GetUserContext();
            var jugadores = await _jugadorService.GetByEquipoIdDetalladoAsync(equipoId, usuarioId, rol);
            return Ok(jugadores);
        }

        [HttpPost("equipos/{equipoId}/jugadores")]
        [Authorize(Roles = "Capitan,Organizador,Administrador")]
        public async Task<IActionResult> Create(Guid equipoId, [FromBody] JugadorCreateDTO dto)
        {
            dto.EquipoId = equipoId;
            var (usuarioId, rol) = GetUserContext();
            var jugador = await _jugadorService.CreateAsync(dto, usuarioId, rol);
            return Ok(jugador);
        }

        [HttpDelete("jugadores/{id}")]
        [Authorize(Roles = "Capitan,Organizador,Administrador")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var (usuarioId, rol) = GetUserContext();
            await _jugadorService.DeleteAsync(id, usuarioId, rol);
            return NoContent();
        }

        [HttpGet("jugadores/{id}/estadisticas")]
        public async Task<IActionResult> GetEstadisticas(Guid id)
        {
            var estadisticas = await _jugadorService.GetEstadisticasAsync(id);
            return Ok(estadisticas);
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
