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

    public class RecuperarPasswordRequestDTO
    {
        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress(ErrorMessage = "El formato de correo electrónico es inválido.")]
        public string Email { get; set; } = string.Empty;
    }

    public class RecuperarPasswordResponseDTO
    {
        public string Mensaje { get; set; } = string.Empty;
        public string? Token { get; set; }
    }

    public class ResetPasswordRequestDTO
    {
        [Required(ErrorMessage = "El token de recuperación es requerido.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es requerida.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*\d).+$", ErrorMessage = "La contraseña debe contener al menos una letra y un número.")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ResetPasswordResponseDTO
    {
        public string Mensaje { get; set; } = string.Empty;
    }
}
