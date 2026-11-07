using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Shared.DTOs
{
    public class JugadorCreateDTO : IValidatableObject
    {
        private static readonly Regex NombrePersonaRegex = new(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", RegexOptions.Compiled);
        private static readonly Regex DniRegex = new(@"^\d{7,8}$", RegexOptions.Compiled);
        private static readonly Regex TelefonoRegex = new(@"^\+?[0-9]{8,15}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s<>]+@[^@\s<>]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex PosicionRegex = new(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s\-]+$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El nombre del jugador es obligatorio.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "El nombre del jugador debe tener entre 2 y 60 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", ErrorMessage = "El nombre del jugador solo puede contener letras y espacios.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido del jugador es obligatorio.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "El apellido del jugador debe tener entre 2 y 60 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$", ErrorMessage = "El apellido del jugador solo puede contener letras y espacios.")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        [RegularExpression(@"^\d{7,8}$", ErrorMessage = "El DNI debe contener exactamente 7 u 8 dígitos numéricos.")]
        public string Dni { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "La URL de documentación no puede exceder los 500 caracteres.")]
        public string? DocumentacionUrl { get; set; }

        public Guid EquipoId { get; set; }

        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public int? Edad { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public int? NumeroCamiseta { get; set; }
        public int? Dorsal { get => NumeroCamiseta; set => NumeroCamiseta = value; }
        public string? Posicion { get; set; }
        public string? FotoUrl { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var nombreTrim = Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreTrim) || nombreTrim.Length < 2 || nombreTrim.Length > 60 || !NombrePersonaRegex.IsMatch(nombreTrim))
            {
                yield return new ValidationResult("El nombre del jugador es obligatorio, debe tener entre 2 y 60 caracteres y contener solo letras y espacios.", new[] { nameof(Nombre) });
            }

            var apellidoTrim = Apellido?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apellidoTrim) || apellidoTrim.Length < 2 || apellidoTrim.Length > 60 || !NombrePersonaRegex.IsMatch(apellidoTrim))
            {
                yield return new ValidationResult("El apellido del jugador es obligatorio, debe tener entre 2 y 60 caracteres y contener solo letras y espacios.", new[] { nameof(Apellido) });
            }

            var dniTrim = Dni?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(dniTrim) || !DniRegex.IsMatch(dniTrim))
            {
                yield return new ValidationResult("El DNI debe contener exactamente 7 u 8 dígitos numéricos.", new[] { nameof(Dni) });
            }

            if (DocumentacionUrl != null)
            {
                var docTrim = DocumentacionUrl.Trim();
                if (string.IsNullOrWhiteSpace(docTrim) || docTrim.Length > 500 || docTrim.Contains('<') || docTrim.Contains('>') || docTrim.Contains(' '))
                {
                    yield return new ValidationResult("La URL de documentación del jugador no es válida.", new[] { nameof(DocumentacionUrl) });
                }
            }

            if (FotoUrl != null)
            {
                var fotoTrim = FotoUrl.Trim();
                if (string.IsNullOrWhiteSpace(fotoTrim) || fotoTrim.Length > 500 || fotoTrim.Contains('<') || fotoTrim.Contains('>') || fotoTrim.Contains(' '))
                {
                    yield return new ValidationResult("La URL de la foto del jugador no es válida.", new[] { nameof(FotoUrl) });
                }
            }

            if (Telefono != null)
            {
                var telTrim = Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !TelefonoRegex.IsMatch(telTrim))
                {
                    yield return new ValidationResult("El teléfono del jugador debe contener entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').", new[] { nameof(Telefono) });
                }
            }

            if (Email != null)
            {
                var emailTrim = Email.Trim();
                if (string.IsNullOrWhiteSpace(emailTrim) || emailTrim.Length > 150 || !EmailRegex.IsMatch(emailTrim))
                {
                    yield return new ValidationResult("El correo electrónico del jugador no tiene un formato válido.", new[] { nameof(Email) });
                }
            }

            if (Edad.HasValue && (Edad.Value < 5 || Edad.Value > 90))
            {
                yield return new ValidationResult("La edad del jugador debe estar entre 5 y 90 años.", new[] { nameof(Edad) });
            }

            if (FechaNacimiento.HasValue)
            {
                var hoy = DateTime.Today;
                var edadCalculada = hoy.Year - FechaNacimiento.Value.Year;
                if (FechaNacimiento.Value.Date > hoy.AddYears(-edadCalculada))
                    edadCalculada--;

                if (FechaNacimiento.Value == default || FechaNacimiento.Value.Date > hoy || edadCalculada < 5 || edadCalculada > 90)
                {
                    yield return new ValidationResult("La fecha de nacimiento del jugador debe corresponder a una edad entre 5 y 90 años.", new[] { nameof(FechaNacimiento) });
                }
            }

            if (NumeroCamiseta.HasValue && (NumeroCamiseta.Value < 1 || NumeroCamiseta.Value > 99))
            {
                yield return new ValidationResult("El número de camiseta o dorsal debe estar entre 1 y 99.", new[] { nameof(NumeroCamiseta) });
            }

            if (Posicion != null)
            {
                var posTrim = Posicion.Trim();
                if (string.IsNullOrWhiteSpace(posTrim) || posTrim.Length < 2 || posTrim.Length > 40 || !PosicionRegex.IsMatch(posTrim))
                {
                    yield return new ValidationResult("La posición del jugador debe tener entre 2 y 40 caracteres alfabéticos.", new[] { nameof(Posicion) });
                }
            }
        }
    }

    public class JugadorResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string? DocumentacionUrl { get; set; }
        public Guid EquipoId { get; set; }
    }
}
