using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Shared.DTOs
{
    public class EquipoCreateDTO : IValidatableObject
    {
        private static readonly Regex ValidEquipoNombreRegex = new(@"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)]+$", RegexOptions.Compiled);
        private static readonly Regex TelefonoRegex = new(@"^\+?[0-9]{8,15}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s<>]+@[^@\s<>]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre del equipo debe tener entre 3 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "La URL del logo no puede exceder los 500 caracteres.")]
        public string? LogoUrl { get; set; }
        public string? Logo { get => LogoUrl; set => LogoUrl = value; }

        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? ComprobanteUrl { get; set; }
        public string? Comprobante { get => ComprobanteUrl; set => ComprobanteUrl = value; }

        public Guid TorneoId { get; set; }
        public Guid? CapitanId { get; set; }

        [Required(ErrorMessage = "La lista de jugadores es obligatoria.")]
        public List<JugadorCreateDTO> Jugadores { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var nombreTrim = Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreTrim) || nombreTrim.Length < 3 || nombreTrim.Length > 100)
            {
                yield return new ValidationResult("El nombre del equipo debe tener entre 3 y 100 caracteres.", new[] { nameof(Nombre) });
            }
            else if (!ValidEquipoNombreRegex.IsMatch(nombreTrim))
            {
                yield return new ValidationResult("El nombre del equipo contiene caracteres no permitidos.", new[] { nameof(Nombre) });
            }

            if (LogoUrl != null)
            {
                var logoTrim = LogoUrl.Trim();
                if (string.IsNullOrWhiteSpace(logoTrim) || logoTrim.Length > 500 || logoTrim.Contains('<') || logoTrim.Contains('>') || logoTrim.Contains(' '))
                {
                    yield return new ValidationResult("La URL del logo no es válida.", new[] { nameof(LogoUrl) });
                }
            }

            if (ComprobanteUrl != null)
            {
                var compTrim = ComprobanteUrl.Trim();
                if (string.IsNullOrWhiteSpace(compTrim) || compTrim.Length > 500 || compTrim.Contains('<') || compTrim.Contains('>') || compTrim.Contains(' '))
                {
                    yield return new ValidationResult("La URL del comprobante no es válida.", new[] { nameof(ComprobanteUrl) });
                }
            }

            if (Telefono != null)
            {
                var telTrim = Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !TelefonoRegex.IsMatch(telTrim))
                {
                    yield return new ValidationResult("El teléfono debe contener entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').", new[] { nameof(Telefono) });
                }
            }

            if (Email != null)
            {
                var emailTrim = Email.Trim();
                if (string.IsNullOrWhiteSpace(emailTrim) || emailTrim.Length > 150 || !EmailRegex.IsMatch(emailTrim))
                {
                    yield return new ValidationResult("El formato del correo electrónico no es válido.", new[] { nameof(Email) });
                }
            }

            if (Jugadores == null)
            {
                yield return new ValidationResult("La lista de jugadores es obligatoria.", new[] { nameof(Jugadores) });
            }
            else
            {
                if (Jugadores.Count > 30)
                {
                    yield return new ValidationResult("La lista de jugadores no puede superar el máximo de 30 integrantes.", new[] { nameof(Jugadores) });
                }

                for (int i = 0; i < Jugadores.Count; i++)
                {
                    if (Jugadores[i] == null)
                    {
                        yield return new ValidationResult("La lista de jugadores no puede contener elementos nulos.", new[] { nameof(Jugadores) });
                        break;
                    }
                }
            }
        }
    }

    public class EquipoUpdateDTO : IValidatableObject
    {
        private static readonly Regex ValidEquipoNombreRegex = new(@"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)]+$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre del equipo debe tener entre 3 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "La URL del logo no puede exceder los 500 caracteres.")]
        public string? Logo { get; set; }

        [StringLength(500, ErrorMessage = "La URL del comprobante no puede exceder los 500 caracteres.")]
        public string? ComprobanteUrl { get; set; }
        public string? Comprobante { get => ComprobanteUrl; set => ComprobanteUrl = value; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var nombreTrim = Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreTrim) || nombreTrim.Length < 3 || nombreTrim.Length > 100 || !ValidEquipoNombreRegex.IsMatch(nombreTrim))
            {
                yield return new ValidationResult("El nombre del equipo debe tener entre 3 y 100 caracteres válidos.", new[] { nameof(Nombre) });
            }

            if (Logo != null)
            {
                var logoTrim = Logo.Trim();
                if (string.IsNullOrWhiteSpace(logoTrim) || logoTrim.Length > 500 || logoTrim.Contains('<') || logoTrim.Contains('>') || logoTrim.Contains(' '))
                {
                    yield return new ValidationResult("La URL del logo no es válida.", new[] { nameof(Logo) });
                }
            }

            var comp = ComprobanteUrl ?? Comprobante;
            if (comp != null)
            {
                var compTrim = comp.Trim();
                if (string.IsNullOrWhiteSpace(compTrim) || compTrim.Length > 500 || compTrim.Contains('<') || compTrim.Contains('>') || compTrim.Contains(' '))
                {
                    yield return new ValidationResult("La URL del comprobante no es válida.", new[] { nameof(ComprobanteUrl) });
                }
            }
        }
    }

    public class EquipoResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string? LogoUrl { get => Logo; set => Logo = value; }
        public string? ComprobanteUrl { get; set; }
        public string? Comprobante { get => ComprobanteUrl; set => ComprobanteUrl = value; }
        public bool PagoValidado { get; set; }
        public bool EdicionListaHabilitada { get; set; }
        public string? MotivoRechazo { get; set; }
        public string EstadoInscripcion { get; set; } = string.Empty;
        public Guid TorneoId { get; set; }
        public Guid? CapitanId { get; set; }
        public int CantidadJugadores { get; set; }
        public List<JugadorResponseDTO> Jugadores { get; set; } = new();
    }

    public class EquipoPublicResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string? LogoUrl { get => Logo; set => Logo = value; }
        public string EstadoInscripcion { get; set; } = string.Empty;
        public Guid TorneoId { get; set; }
        public int CantidadJugadores { get; set; }
        public List<JugadorPublicResponseDTO> Jugadores { get; set; } = new();
    }
}
