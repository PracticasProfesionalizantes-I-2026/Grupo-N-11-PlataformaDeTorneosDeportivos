using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class CargarResultadoDTO : IValidatableObject
    {
        [Range(0, 999, ErrorMessage = "El marcador del equipo local debe estar entre 0 y 999.")]
        public int? MarcadorLocal { get; set; }

        [Range(0, 999, ErrorMessage = "El marcador del equipo visitante debe estar entre 0 y 999.")]
        public int? MarcadorVisitante { get; set; }

        [Range(0, 999, ErrorMessage = "El marcador del equipo A debe estar entre 0 y 999.")]
        public int? MarcadorA { get; set; }

        [Range(0, 999, ErrorMessage = "El marcador del equipo B debe estar entre 0 y 999.")]
        public int? MarcadorB { get; set; }

        [Range(0, 999, ErrorMessage = "Los goles del equipo local deben estar entre 0 y 999.")]
        public int? GolesLocal { get; set; }

        [Range(0, 999, ErrorMessage = "Los goles del equipo visitante deben estar entre 0 y 999.")]
        public int? GolesVisitante { get; set; }

        [Range(0, 999, ErrorMessage = "Los puntos del equipo local deben estar entre 0 y 999.")]
        public int? PuntosLocal { get; set; }

        [Range(0, 999, ErrorMessage = "Los puntos del equipo visitante deben estar entre 0 y 999.")]
        public int? PuntosVisitante { get; set; }

        [Range(0, 999, ErrorMessage = "Los penales del equipo local deben estar entre 0 y 999.")]
        public int? PenalesLocal { get; set; }

        [Range(0, 999, ErrorMessage = "Los penales del equipo visitante deben estar entre 0 y 999.")]
        public int? PenalesVisitante { get; set; }

        [Range(0, 999, ErrorMessage = "Los penales del equipo A deben estar entre 0 y 999.")]
        public int? PenalesEquipoA { get; set; }

        [Range(0, 999, ErrorMessage = "Los penales del equipo B deben estar entre 0 y 999.")]
        public int? PenalesEquipoB { get; set; }

        public int? ObtenerMarcadorLocal() => MarcadorLocal ?? MarcadorA ?? GolesLocal ?? PuntosLocal;
        public int? ObtenerMarcadorVisitante() => MarcadorVisitante ?? MarcadorB ?? GolesVisitante ?? PuntosVisitante;
        public int? ObtenerPenalesLocal() => PenalesLocal ?? PenalesEquipoA;
        public int? ObtenerPenalesVisitante() => PenalesVisitante ?? PenalesEquipoB;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var ml = ObtenerMarcadorLocal();
            var mv = ObtenerMarcadorVisitante();

            if (!ml.HasValue || !mv.HasValue)
            {
                yield return new ValidationResult("Debe ingresar los marcadores de ambos equipos.", new[] { nameof(MarcadorLocal) });
            }
            else if (ml.Value < 0 || ml.Value > 999 || mv.Value < 0 || mv.Value > 999)
            {
                yield return new ValidationResult("Los marcadores deben estar entre 0 y 999.", new[] { nameof(MarcadorLocal) });
            }

            var pl = ObtenerPenalesLocal();
            var pv = ObtenerPenalesVisitante();

            if (pl.HasValue != pv.HasValue)
            {
                yield return new ValidationResult("Si se informan penales, deben especificarse para ambos equipos.", new[] { nameof(PenalesLocal) });
            }
            else if (pl.HasValue && pv.HasValue && (pl.Value < 0 || pl.Value > 999 || pv.Value < 0 || pv.Value > 999))
            {
                yield return new ValidationResult("Los penales deben estar entre 0 y 999.", new[] { nameof(PenalesLocal) });
            }
        }
    }

    public class PartidoResultadoResponseDTO
    {
        public Guid Id { get; set; }
        public Guid TorneoId { get; set; }
        public string TorneoNombre { get; set; } = string.Empty;
        public Guid? EquipoLocalId { get; set; }
        public string EquipoLocalNombre { get; set; } = string.Empty;
        public Guid? EquipoVisitanteId { get; set; }
        public string EquipoVisitanteNombre { get; set; } = string.Empty;
        public int MarcadorLocal { get; set; }
        public int MarcadorVisitante { get; set; }
        public int? PenalesLocal { get; set; }
        public int? PenalesVisitante { get; set; }
        public string Estado { get; set; } = string.Empty;
        public Guid? GanadorId { get; set; }
        public string? GanadorNombre { get; set; }
        public string Mensaje { get; set; } = "Marcador y estado del partido guardados exitosamente.";
    }
}
