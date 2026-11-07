using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.DTOs
{
    public class IncidenciaCreateDTO : IValidatableObject
    {
        [Required(ErrorMessage = "El tipo de incidencia es obligatorio.")]
        [EnumDataType(typeof(TipoIncidencia), ErrorMessage = "El tipo de incidencia especificado no es válido.")]
        public TipoIncidencia TipoIncidencia { get; set; }

        public TipoIncidencia? Tipo { get; set; }

        [Range(1, 150, ErrorMessage = "El minuto debe encontrarse entre 1 y 150.")]
        public int Minuto { get; set; }

        [Required(ErrorMessage = "El identificador del jugador es obligatorio.")]
        public Guid JugadorId { get; set; }

        [Required(ErrorMessage = "El identificador del equipo es obligatorio.")]
        public Guid EquipoId { get; set; }

        [StringLength(300, ErrorMessage = "Las observaciones no pueden superar los 300 caracteres.")]
        public string? Observaciones { get; set; }

        [StringLength(300, ErrorMessage = "La observación no puede superar los 300 caracteres.")]
        public string? Observacion { get; set; }

        [StringLength(300, ErrorMessage = "La descripción no puede superar los 300 caracteres.")]
        public string? Descripcion { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Tipo.HasValue && !Enum.IsDefined(typeof(TipoIncidencia), Tipo.Value))
            {
                yield return new ValidationResult("El tipo de incidencia especificado no es válido.", new[] { nameof(Tipo) });
            }

            if (!Enum.IsDefined(typeof(TipoIncidencia), TipoIncidencia))
            {
                yield return new ValidationResult("El tipo de incidencia especificado no es válido.", new[] { nameof(TipoIncidencia) });
            }

            if (Minuto < 1 || Minuto > 150)
            {
                yield return new ValidationResult("El minuto de la incidencia debe estar entre 1 y 150.", new[] { nameof(Minuto) });
            }

            if (JugadorId == Guid.Empty)
            {
                yield return new ValidationResult("El identificador del jugador no puede ser vacío.", new[] { nameof(JugadorId) });
            }

            if (EquipoId == Guid.Empty)
            {
                yield return new ValidationResult("El identificador del equipo no puede ser vacío.", new[] { nameof(EquipoId) });
            }

            var obs = Observaciones ?? Observacion ?? Descripcion;
            if (obs != null)
            {
                var obsTrim = obs.Trim();
                if (string.IsNullOrWhiteSpace(obsTrim) || obsTrim.Length > 300 || obsTrim.Contains('<') || obsTrim.Contains('>'))
                {
                    yield return new ValidationResult("Las observaciones/descripción no pueden estar vacías, superar los 300 caracteres ni contener etiquetas HTML.", new[] { nameof(Observaciones) });
                }
            }
        }
    }

    public class IncidenciaResponseDTO
    {
        public Guid Id { get; set; }
        public Guid PartidoId { get; set; }
        public Guid EquipoId { get; set; }
        public string EquipoNombre { get; set; } = string.Empty;
        public Guid JugadorId { get; set; }
        public string JugadorNombreCompleto { get; set; } = string.Empty;
        public string TipoIncidencia { get; set; } = string.Empty;
        public int Minuto { get; set; }
        public string? Observaciones { get; set; }
        public string EstadoDisciplinarioJugador { get; set; } = "Habilitado";
        public bool JugadorExpulsado { get; set; }
        public int MarcadorLocalParcial { get; set; }
        public int MarcadorVisitanteParcial { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string Mensaje { get; set; } = "Incidencia registrada exitosamente.";
    }
}
