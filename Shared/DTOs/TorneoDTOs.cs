using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Shared.Enums;

namespace Shared.DTOs
{
    public class TorneoCreateDTO : IValidatableObject
    {
        private static readonly Regex ValidTorneoNombreRegex = new(@"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)\/]+$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El nombre del torneo es requerido.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public Disciplina Disciplina { get; set; }
        public int? DisciplinaId { get; set; }
        public Modalidad Modalidad { get; set; }
        public int? ModalidadId { get; set; }
        public int CupoMaximo { get; set; }
        public bool EsPago { get; set; }
        public decimal CostoInscripcion { get; set; }
        public bool? CuentaRecaudadoraActiva { get; set; }
        public string? Descripcion { get; set; }
        public string? Reglamento { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var nombreTrim = Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreTrim) || nombreTrim.Length < 3 || nombreTrim.Length > 100)
            {
                yield return new ValidationResult("El nombre del torneo es obligatorio y debe tener entre 3 y 100 caracteres.", new[] { nameof(Nombre) });
            }
            else if (!ValidTorneoNombreRegex.IsMatch(nombreTrim))
            {
                yield return new ValidationResult("El nombre del torneo contiene caracteres no válidos.", new[] { nameof(Nombre) });
            }

            if (!Enum.IsDefined(typeof(Modalidad), Modalidad))
            {
                yield return new ValidationResult("La modalidad especificada no es válida.", new[] { nameof(Modalidad) });
            }

            if (ModalidadId.HasValue && !Enum.IsDefined(typeof(Modalidad), ModalidadId.Value))
            {
                yield return new ValidationResult("El identificador de modalidad especificado no es válido.", new[] { nameof(ModalidadId) });
            }

            if (CostoInscripcion < 0 || CostoInscripcion > 100_000_000m)
            {
                yield return new ValidationResult("El costo de inscripción no puede ser negativo ni superar los 100.000.000.", new[] { nameof(CostoInscripcion) });
            }
            else if (!EsPago && CostoInscripcion != 0)
            {
                yield return new ValidationResult("No se puede ingresar un costo de inscripción si el torneo es gratuito (EsPago = false)", new[] { nameof(CostoInscripcion) });
            }
            else if (EsPago && CostoInscripcion <= 0)
            {
                yield return new ValidationResult("El costo debe ser mayor a cero para torneos pagos", new[] { nameof(CostoInscripcion) });
            }

            if (Descripcion != null)
            {
                var descTrim = Descripcion.Trim();
                if (string.IsNullOrWhiteSpace(descTrim) || descTrim.Length > 1000 || descTrim.Contains('<') || descTrim.Contains('>'))
                {
                    yield return new ValidationResult("La descripción no puede estar vacía, superar los 1000 caracteres ni contener etiquetas HTML.", new[] { nameof(Descripcion) });
                }
            }

            if (Reglamento != null)
            {
                var regTrim = Reglamento.Trim();
                if (string.IsNullOrWhiteSpace(regTrim) || regTrim.Length > 2000 || regTrim.Contains('<') || regTrim.Contains('>'))
                {
                    yield return new ValidationResult("El reglamento no puede estar vacío, superar los 2000 caracteres ni contener etiquetas HTML.", new[] { nameof(Reglamento) });
                }
            }

            if (FechaInicio.HasValue)
            {
                if (FechaInicio.Value == default || FechaInicio.Value.Date < DateTime.Today || FechaInicio.Value.Year > 2100)
                {
                    yield return new ValidationResult("La fecha de inicio del torneo no puede estar en el pasado ni ser inválida.", new[] { nameof(FechaInicio) });
                }
            }

            if (FechaFin.HasValue)
            {
                if (FechaFin.Value == default || FechaFin.Value.Date < DateTime.Today || FechaFin.Value.Year > 2100)
                {
                    yield return new ValidationResult("La fecha de finalización del torneo no puede estar en el pasado ni ser inválida.", new[] { nameof(FechaFin) });
                }
                else if (FechaInicio.HasValue && FechaFin.Value.Date < FechaInicio.Value.Date)
                {
                    yield return new ValidationResult("La fecha de finalización no puede ser anterior a la fecha de inicio.", new[] { nameof(FechaFin) });
                }
            }
        }
    }

    public class TorneoUpdateDTO : IValidatableObject
    {
        private static readonly Regex ValidTorneoNombreRegex = new(@"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)\/]+$", RegexOptions.Compiled);

        [Required(ErrorMessage = "El nombre del torneo es requerido.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public Disciplina Disciplina { get; set; }
        public int? DisciplinaId { get; set; }
        public Modalidad Modalidad { get; set; }
        public int? ModalidadId { get; set; }
        public int CupoMaximo { get; set; }
        public bool EsPago { get; set; }
        public decimal CostoInscripcion { get; set; }
        public bool? CuentaRecaudadoraActiva { get; set; }
        public string? Descripcion { get; set; }
        public string? Reglamento { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var nombreTrim = Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreTrim) || nombreTrim.Length < 3 || nombreTrim.Length > 100)
            {
                yield return new ValidationResult("El nombre del torneo es obligatorio y debe tener entre 3 y 100 caracteres.", new[] { nameof(Nombre) });
            }
            else if (!ValidTorneoNombreRegex.IsMatch(nombreTrim))
            {
                yield return new ValidationResult("El nombre del torneo contiene caracteres no válidos.", new[] { nameof(Nombre) });
            }

            if (!Enum.IsDefined(typeof(Modalidad), Modalidad))
            {
                yield return new ValidationResult("La modalidad especificada no es válida.", new[] { nameof(Modalidad) });
            }

            if (ModalidadId.HasValue && !Enum.IsDefined(typeof(Modalidad), ModalidadId.Value))
            {
                yield return new ValidationResult("El identificador de modalidad especificado no es válido.", new[] { nameof(ModalidadId) });
            }

            if (CostoInscripcion < 0 || CostoInscripcion > 100_000_000m)
            {
                yield return new ValidationResult("El costo de inscripción no puede ser negativo ni superar los 100.000.000.", new[] { nameof(CostoInscripcion) });
            }
            else if (!EsPago && CostoInscripcion != 0)
            {
                yield return new ValidationResult("No se puede ingresar un costo de inscripción si el torneo es gratuito (EsPago = false)", new[] { nameof(CostoInscripcion) });
            }
            else if (EsPago && CostoInscripcion <= 0)
            {
                yield return new ValidationResult("El costo debe ser mayor a cero para torneos pagos", new[] { nameof(CostoInscripcion) });
            }
        }
    }

    public class TorneoResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Disciplina { get; set; } = string.Empty;
        public string Modalidad { get; set; } = string.Empty;
        public int CupoMaximo { get; set; }
        public bool EsPago { get; set; }
        public decimal CostoInscripcion { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
