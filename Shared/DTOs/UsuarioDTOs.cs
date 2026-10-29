using System;
using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.DTOs
{
    public class UsuarioRegistroDTO
    {
        [Required(ErrorMessage = "El nombre es requerido.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es requerido.")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es requerido.")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "El DNI debe contener solo números.")]
        public string Dni { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es requerido.")]
        [EmailAddress(ErrorMessage = "El formato de correo electrónico es inválido.")]
        public string Email { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*\d).+$", ErrorMessage = "La contraseña debe contener al menos una letra y un número.")]
        public string Password { get; set; } = string.Empty;

        public RolUsuario Rol { get; set; } = RolUsuario.Capitan;
    }

    public class UsuarioResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string Rol { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}
