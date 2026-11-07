using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class RechazarInscripcionDTO : IValidatableObject
    {
        [Required(ErrorMessage = "El motivo de rechazo es obligatorio.")]
        [StringLength(500, MinimumLength = 3, ErrorMessage = "El motivo debe tener entre 3 y 500 caracteres.")]
        public string MotivoRechazo { get; set; } = string.Empty;

        public string? Motivo { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var rawMotivo = !string.IsNullOrWhiteSpace(MotivoRechazo) ? MotivoRechazo : Motivo;
            var motivoTrim = rawMotivo?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(motivoTrim) || motivoTrim.Length < 3 || motivoTrim.Length > 500)
            {
                yield return new ValidationResult("El motivo de rechazo es obligatorio y debe tener entre 3 y 500 caracteres.", new[] { nameof(MotivoRechazo) });
            }
            else if (motivoTrim.Contains('<') || motivoTrim.Contains('>'))
            {
                yield return new ValidationResult("El motivo de rechazo contiene caracteres no permitidos.", new[] { nameof(MotivoRechazo) });
            }
        }
    }

    public class PagoInscripcionDTO : IValidatableObject
    {
        [Required(ErrorMessage = "El comprobante de pago es obligatorio.")]
        [StringLength(500, MinimumLength = 3, ErrorMessage = "El comprobante debe tener entre 3 y 500 caracteres.")]
        public string ComprobanteUrl { get; set; } = string.Empty;

        [Range(0.01, 100000000, ErrorMessage = "El monto debe ser mayor a 0 y no superar 100.000.000.")]
        public decimal Monto { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var compTrim = ComprobanteUrl?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(compTrim) || compTrim.Length < 3 || compTrim.Length > 500 || compTrim.Contains('<') || compTrim.Contains('>'))
            {
                yield return new ValidationResult("El comprobante de pago es obligatorio y debe tener entre 3 y 500 caracteres válidos.", new[] { nameof(ComprobanteUrl) });
            }

            if (Monto <= 0 || Monto > 100_000_000m)
            {
                yield return new ValidationResult("El monto del pago debe ser mayor a cero y no superar los 100.000.000.", new[] { nameof(Monto) });
            }
        }
    }

    public class AprobarInscripcionResponseDTO
    {
        public Guid EquipoId { get; set; }
        public string NombreEquipo { get; set; } = string.Empty;
        public string EstadoInscripcion { get; set; } = string.Empty;
        public Guid TorneoId { get; set; }
        public int CuposDisponibles { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }

    public class RechazarInscripcionResponseDTO
    {
        public Guid EquipoId { get; set; }
        public string NombreEquipo { get; set; } = string.Empty;
        public string EstadoInscripcion { get; set; } = string.Empty;
        public Guid TorneoId { get; set; }
        public string MotivoRechazo { get; set; } = string.Empty;
        public bool EdicionListaHabilitada { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}
