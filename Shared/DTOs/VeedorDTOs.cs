using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shared.DTOs
{
    public class VeedorCreateDTO : IValidatableObject
    {
        private static readonly Regex NombrePersonaRegex = new(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", RegexOptions.Compiled);
        private static readonly Regex TelefonoRegex = new(@"^\+?[0-9]{8,15}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s<>]+@[^@\s<>]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex SedeRegex = new(@"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)]+$", RegexOptions.Compiled);
        private static readonly Regex PasswordComplexityRegex = new(@"^(?=.*[a-zA-Z])(?=.*\d).+$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 60 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 60 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", ErrorMessage = "El apellido solo puede contener letras y espacios.")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        [RegularExpression(@"^\d{7,8}$", ErrorMessage = "El DNI debe contener exactamente 7 u 8 dígitos numéricos.")]
        public string Dni { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [StringLength(150, ErrorMessage = "El correo electrónico no puede exceder los 150 caracteres.")]
        [EmailAddress(ErrorMessage = "El formato de correo electrónico es inválido.")]
        [RegularExpression(@"^[^@\s<>]+@[^@\s<>]+\.[a-zA-Z]{2,}$", ErrorMessage = "El formato de correo electrónico es inválido.")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(@"^\+?[0-9]{8,15}$", ErrorMessage = "El teléfono debe contener entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').")]
        public string? Telefono { get; set; }

        public List<Guid>? TorneosAsignados { get; set; }

        [StringLength(100, MinimumLength = 2, ErrorMessage = "La sede asignada debe tener entre 2 y 100 caracteres.")]
        public string? SedeAsignada { get; set; }

        [StringLength(128, MinimumLength = 8, ErrorMessage = "La contraseña inicial debe tener entre 8 y 128 caracteres.")]
        [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*\d).+$", ErrorMessage = "La contraseña inicial debe contener al menos una letra y un número.")]
        public string? PasswordInicial { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var nombreTrim = Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreTrim) || nombreTrim.Length < 2 || nombreTrim.Length > 60 || !NombrePersonaRegex.IsMatch(nombreTrim))
            {
                yield return new ValidationResult("El nombre del veedor debe tener entre 2 y 60 caracteres y contener solo letras y espacios.", new[] { nameof(Nombre) });
            }

            var apellidoTrim = Apellido?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apellidoTrim) || apellidoTrim.Length < 2 || apellidoTrim.Length > 60 || !NombrePersonaRegex.IsMatch(apellidoTrim))
            {
                yield return new ValidationResult("El apellido del veedor debe tener entre 2 y 60 caracteres y contener solo letras y espacios.", new[] { nameof(Apellido) });
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

            if (SedeAsignada != null)
            {
                var sedeTrim = SedeAsignada.Trim();
                if (string.IsNullOrWhiteSpace(sedeTrim) || sedeTrim.Length < 2 || sedeTrim.Length > 100 || !SedeRegex.IsMatch(sedeTrim))
                {
                    yield return new ValidationResult("La sede asignada debe tener entre 2 y 100 caracteres válidos.", new[] { nameof(SedeAsignada) });
                }
            }

            if (!string.IsNullOrWhiteSpace(PasswordInicial))
            {
                var pwdTrim = PasswordInicial.Trim();
                if (pwdTrim.Length < 8 || pwdTrim.Length > 128 || !PasswordComplexityRegex.IsMatch(pwdTrim))
                {
                    yield return new ValidationResult("La contraseña inicial debe tener entre 8 y 128 caracteres y contener al menos una letra y un número.", new[] { nameof(PasswordInicial) });
                }
            }

            if (TorneosAsignados != null)
            {
                if (TorneosAsignados.Count > 50)
                {
                    yield return new ValidationResult("No se pueden asignar más de 50 torneos a un veedor.", new[] { nameof(TorneosAsignados) });
                }
                if (TorneosAsignados.Any(t => t == Guid.Empty))
                {
                    yield return new ValidationResult("La lista de torneos asignados no puede contener identificadores vacíos.", new[] { nameof(TorneosAsignados) });
                }
                if (TorneosAsignados.Distinct().Count() < TorneosAsignados.Count)
                {
                    yield return new ValidationResult("La lista de torneos asignados no puede contener identificadores duplicados.", new[] { nameof(TorneosAsignados) });
                }
            }
        }
    }

    public class VeedorUpdateDTO : IValidatableObject
    {
        private static readonly Regex NombrePersonaRegex = new(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", RegexOptions.Compiled);
        private static readonly Regex TelefonoRegex = new(@"^\+?[0-9]{8,15}$", RegexOptions.Compiled);
        private static readonly Regex SedeRegex = new(@"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)]+$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 60 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 60 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", ErrorMessage = "El apellido solo puede contener letras y espacios.")]
        public string Apellido { get; set; } = string.Empty;

        [RegularExpression(@"^\+?[0-9]{8,15}$", ErrorMessage = "El teléfono debe contener entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').")]
        public string? Telefono { get; set; }

        public List<Guid>? TorneosAsignados { get; set; }

        [StringLength(100, MinimumLength = 2, ErrorMessage = "La sede asignada debe tener entre 2 y 100 caracteres.")]
        public string? SedeAsignada { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var nombreTrim = Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreTrim) || nombreTrim.Length < 2 || nombreTrim.Length > 60 || !NombrePersonaRegex.IsMatch(nombreTrim))
            {
                yield return new ValidationResult("El nombre del veedor debe tener entre 2 y 60 caracteres y contener solo letras y espacios.", new[] { nameof(Nombre) });
            }

            var apellidoTrim = Apellido?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apellidoTrim) || apellidoTrim.Length < 2 || apellidoTrim.Length > 60 || !NombrePersonaRegex.IsMatch(apellidoTrim))
            {
                yield return new ValidationResult("El apellido del veedor debe tener entre 2 y 60 caracteres y contener solo letras y espacios.", new[] { nameof(Apellido) });
            }

            if (Telefono != null)
            {
                var telTrim = Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !TelefonoRegex.IsMatch(telTrim))
                {
                    yield return new ValidationResult("El teléfono debe contener entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').", new[] { nameof(Telefono) });
                }
            }

            if (SedeAsignada != null)
            {
                var sedeTrim = SedeAsignada.Trim();
                if (string.IsNullOrWhiteSpace(sedeTrim) || sedeTrim.Length < 2 || sedeTrim.Length > 100 || !SedeRegex.IsMatch(sedeTrim))
                {
                    yield return new ValidationResult("La sede asignada debe tener entre 2 y 100 caracteres válidos.", new[] { nameof(SedeAsignada) });
                }
            }

            if (TorneosAsignados != null)
            {
                if (TorneosAsignados.Count > 50)
                {
                    yield return new ValidationResult("No se pueden asignar más de 50 torneos a un veedor.", new[] { nameof(TorneosAsignados) });
                }
                if (TorneosAsignados.Any(t => t == Guid.Empty))
                {
                    yield return new ValidationResult("La lista de torneos asignados no puede contener identificadores vacíos.", new[] { nameof(TorneosAsignados) });
                }
                if (TorneosAsignados.Distinct().Count() < TorneosAsignados.Count)
                {
                    yield return new ValidationResult("La lista de torneos asignados no puede contener identificadores duplicados.", new[] { nameof(TorneosAsignados) });
                }
            }
        }
    }

    public class VeedorResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string Rol { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public List<Guid> TorneosAsignados { get; set; } = new();
        public string? SedeAsignada { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string? PasswordTemporal { get; set; }
        public bool RequiereCambioPassword { get; set; }
    }

    public class PartidoCronogramaResponseDTO
    {
        public Guid Id { get; set; }
        public Guid TorneoId { get; set; }
        public string TorneoNombre { get; set; } = string.Empty;
        public Guid? EquipoLocalId { get; set; }
        public string EquipoLocalNombre { get; set; } = string.Empty;
        public Guid? EquipoVisitanteId { get; set; }
        public string EquipoVisitanteNombre { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public string Sede { get; set; } = string.Empty;
        public int Ronda { get; set; }
        public string Estado { get; set; } = string.Empty;
        public Guid? VeedorId { get; set; }
    }
}
