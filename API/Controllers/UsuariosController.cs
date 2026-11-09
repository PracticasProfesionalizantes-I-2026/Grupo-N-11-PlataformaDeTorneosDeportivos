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
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;

        public UsuariosController(IUsuarioService usuarioService)
        {
            _usuarioService = usuarioService;
        }

        [HttpPost("registro")]
        [HttpPost("/api/auth/register")]
        [AllowAnonymous]
        public async Task<IActionResult> Registro([FromBody] UsuarioRegistroDTO dto)
        {
            var result = await _usuarioService.RegisterUsuarioAsync(dto);
            return StatusCode(201, result);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Administrador,Organizador")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _usuarioService.GetByIdAsync(id);
            return Ok(result);
        }

        [HttpPost("admin")]
        [Authorize(Roles = "Administrador,Organizador")]
        public async Task<IActionResult> CreateUsuarioAdmin([FromBody] UsuarioRegistroDTO dto)
        {
            var (usuarioId, rol) = GetUserContext();
            var result = await _usuarioService.CreateUsuarioAdminAsync(dto, rol);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id}/rol")]
        [Authorize(Roles = "Administrador,Organizador")]
        public async Task<IActionResult> CambiarRol(Guid id, [FromBody] CambiarRolDTO dto)
        {
            var (usuarioId, rol) = GetUserContext();
            var result = await _usuarioService.CambiarRolAsync(id, dto, rol);
            return Ok(result);
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
