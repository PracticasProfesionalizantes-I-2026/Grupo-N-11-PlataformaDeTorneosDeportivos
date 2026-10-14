using System;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class LoginRequestDTO
    {
        [Required(ErrorMessage = "El email o DNI es requerido.")]
        public string EmailOrDni { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida.")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponseDTO
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public Guid UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
    }
}
