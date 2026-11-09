using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Shared.DTOs
{
    public class LoginRequestDTO : IValidatableObject
    {
        private static readonly Regex DniRegex = new(@"^\d{7,8}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s<>]+@[^@\s<>]+\.[^@\s<>]+$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El email o DNI es requerido.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "El email o DNI debe tener entre 3 y 150 caracteres.")]
        public string EmailOrDni { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida.")]
        [StringLength(128, MinimumLength = 1, ErrorMessage = "La contraseña no puede estar vacía ni exceder los 128 caracteres.")]
        public string Password { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var cred = EmailOrDni?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cred) || cred.Length > 150)
            {
                yield return new ValidationResult("El email o DNI es obligatorio.", new[] { nameof(EmailOrDni) });
            }
            else if (!DniRegex.IsMatch(cred) && !EmailRegex.IsMatch(cred))
            {
                yield return new ValidationResult("Debe ingresar un correo electrónico válido o un DNI numérico de 7 u 8 dígitos.", new[] { nameof(EmailOrDni) });
            }

            if (string.IsNullOrWhiteSpace(Password) || Password.Length > 128)
            {
                yield return new ValidationResult("La contraseña es obligatoria.", new[] { nameof(Password) });
            }
        }
    }

    public class LoginResponseDTO
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public Guid UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public bool RequiereCambioPassword { get; set; } = false;
    }

    public class AuthResponseDTO : LoginResponseDTO
    {
    }

    public class RecuperarPasswordRequestDTO : IValidatableObject
    {
        private static readonly Regex EmailRegex = new(@"^[^@\s<>]+@[^@\s<>]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [StringLength(150, ErrorMessage = "El correo electrónico no puede exceder los 150 caracteres.")]
        [EmailAddress(ErrorMessage = "El formato de correo electrónico es inválido.")]
        [RegularExpression(@"^[^@\s<>]+@[^@\s<>]+\.[a-zA-Z]{2,}$", ErrorMessage = "El formato de correo electrónico es inválido.")]
        public string Email { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var emailTrim = Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(emailTrim) || emailTrim.Length > 150 || !EmailRegex.IsMatch(emailTrim))
            {
                yield return new ValidationResult("El formato de correo electrónico es inválido.", new[] { nameof(Email) });
            }
        }
    }

    public class RecuperarPasswordResponseDTO
    {
        public string Mensaje { get; set; } = string.Empty;
        public string? Token { get; set; }
    }

    public class ResetPasswordRequestDTO : IValidatableObject
    {
        [Required(ErrorMessage = "El token de recuperación es requerido.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "El token de recuperación es inválido.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es requerida.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        [MaxLength(128, ErrorMessage = "La contraseña no puede exceder los 128 caracteres.")]
        [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*\d).+$", ErrorMessage = "La contraseña debe contener al menos una letra y un número.")]
        public string NewPassword { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var tokenTrim = Token?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tokenTrim) || tokenTrim.Length > 200 || tokenTrim.Contains('<') || tokenTrim.Contains('>'))
            {
                yield return new ValidationResult("El token de recuperación es inválido.", new[] { nameof(Token) });
            }

            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8 || NewPassword.Length > 128)
            {
                yield return new ValidationResult("La nueva contraseña debe tener entre 8 y 128 caracteres.", new[] { nameof(NewPassword) });
            }
        }
    }

    public class ResetPasswordResponseDTO
    {
        public string Mensaje { get; set; } = string.Empty;
    }

    public class CambiarPasswordInicialDTO : IValidatableObject
    {
        private static readonly Regex DniRegex = new(@"^\d{7,8}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s<>]+@[^@\s<>]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex PasswordComplexityRegex = new(@"^(?=.*[a-zA-Z])(?=.*\d).+$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El email o DNI es requerido.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "El email o DNI debe tener entre 3 y 150 caracteres.")]
        public string EmailOrDni { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña actual es requerida.")]
        [StringLength(128, MinimumLength = 1, ErrorMessage = "La contraseña actual no puede exceder los 128 caracteres.")]
        public string PasswordActual { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es requerida.")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "La nueva contraseña debe tener entre 8 y 128 caracteres.")]
        [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*\d).+$", ErrorMessage = "La nueva contraseña debe contener al menos una letra y un número.")]
        public string NuevaPassword { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var cred = EmailOrDni?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cred) || cred.Length > 150)
            {
                yield return new ValidationResult("El email o DNI es obligatorio.", new[] { nameof(EmailOrDni) });
            }
            else if (!DniRegex.IsMatch(cred) && !EmailRegex.IsMatch(cred))
            {
                yield return new ValidationResult("Debe ingresar un correo electrónico válido o un DNI numérico de 7 u 8 dígitos.", new[] { nameof(EmailOrDni) });
            }

            if (string.IsNullOrWhiteSpace(PasswordActual))
            {
                yield return new ValidationResult("La contraseña actual es requerida.", new[] { nameof(PasswordActual) });
            }

            var newPwd = NuevaPassword?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 8 || newPwd.Length > 128 || !PasswordComplexityRegex.IsMatch(newPwd))
            {
                yield return new ValidationResult("La nueva contraseña debe tener entre 8 y 128 caracteres y contener al menos una letra y un número.", new[] { nameof(NuevaPassword) });
            }

            if (!string.IsNullOrEmpty(PasswordActual) && PasswordActual == NuevaPassword)
            {
                yield return new ValidationResult("La nueva contraseña no puede ser idéntica a la contraseña provisoria o actual.", new[] { nameof(NuevaPassword) });
            }
        }
    }

    public class CambiarPasswordResponseDTO
    {
        public bool Exito { get; set; } = true;
        public string Mensaje { get; set; } = string.Empty;
        public string? Token { get; set; }
    }
}
