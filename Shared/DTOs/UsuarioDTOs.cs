using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Shared.Enums;

namespace Shared.DTOs
{
    public class UsuarioRegistroDTO : IValidatableObject
    {
        private static readonly Regex NombrePersonaRegex = new(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", RegexOptions.Compiled);
        private static readonly Regex TelefonoRegex = new(@"^\+?[0-9]{8,15}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s<>]+@[^@\s<>]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El nombre es requerido.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 60 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es requerido.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 60 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", ErrorMessage = "El apellido solo puede contener letras y espacios.")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es requerido.")]
        [RegularExpression(@"^\d{7,8}$", ErrorMessage = "El DNI debe contener exactamente 7 u 8 dígitos numéricos.")]
        public string Dni { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es requerido.")]
        [StringLength(150, ErrorMessage = "El correo electrónico no puede exceder los 150 caracteres.")]
        [EmailAddress(ErrorMessage = "El formato de correo electrónico es inválido.")]
        [RegularExpression(@"^[^@\s<>]+@[^@\s<>]+\.[a-zA-Z]{2,}$", ErrorMessage = "El formato de correo electrónico es inválido.")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(@"^\+?[0-9]{8,15}$", ErrorMessage = "El teléfono debe contener entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').")]
        public string? Telefono { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        [MaxLength(128, ErrorMessage = "La contraseña no puede exceder los 128 caracteres.")]
        [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*\d).+$", ErrorMessage = "La contraseña debe contener al menos una letra y un número.")]
        public string Password { get; set; } = string.Empty;

        [EnumDataType(typeof(RolUsuario), ErrorMessage = "El rol de usuario especificado no es válido.")]
        public RolUsuario Rol { get; set; } = RolUsuario.Capitan;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var nombreTrim = Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreTrim) || nombreTrim.Length < 2 || nombreTrim.Length > 60 || !NombrePersonaRegex.IsMatch(nombreTrim))
            {
                yield return new ValidationResult("El nombre debe tener entre 2 y 60 caracteres y contener solo letras y espacios.", new[] { nameof(Nombre) });
            }

            var apellidoTrim = Apellido?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apellidoTrim) || apellidoTrim.Length < 2 || apellidoTrim.Length > 60 || !NombrePersonaRegex.IsMatch(apellidoTrim))
            {
                yield return new ValidationResult("El apellido debe tener entre 2 y 60 caracteres y contener solo letras y espacios.", new[] { nameof(Apellido) });
            }

            var emailTrim = Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(emailTrim) || emailTrim.Length > 150 || !EmailRegex.IsMatch(emailTrim))
            {
                yield return new ValidationResult("El formato de correo electrónico es inválido.", new[] { nameof(Email) });
            }

            if (Telefono != null)
            {
                var telTrim = Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !TelefonoRegex.IsMatch(telTrim))
                {
                    yield return new ValidationResult("El teléfono debe contener entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').", new[] { nameof(Telefono) });
                }
            }

            if (!Enum.IsDefined(typeof(RolUsuario), Rol))
            {
                yield return new ValidationResult("El rol de usuario especificado no es válido.", new[] { nameof(Rol) });
            }
        }
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

    public class CambiarRolDTO : IValidatableObject
    {
        [Required(ErrorMessage = "El nuevo rol es obligatorio.")]
        [EnumDataType(typeof(RolUsuario), ErrorMessage = "El rol de usuario especificado no es válido.")]
        public RolUsuario NuevoRol { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Enum.IsDefined(typeof(RolUsuario), NuevoRol))
            {
                yield return new ValidationResult("El rol de usuario especificado no es válido.", new[] { nameof(NuevoRol) });
            }
        }
    }
}
