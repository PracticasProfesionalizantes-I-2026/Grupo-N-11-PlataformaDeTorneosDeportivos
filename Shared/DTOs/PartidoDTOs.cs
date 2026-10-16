using System;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class CargarResultadoDTO
    {
        [Range(0, int.MaxValue, ErrorMessage = "El marcador del equipo local debe ser un valor no negativo.")]
        public int? MarcadorLocal { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El marcador del equipo visitante debe ser un valor no negativo.")]
        public int? MarcadorVisitante { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El marcador del equipo A debe ser un valor no negativo.")]
        public int? MarcadorA { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El marcador del equipo B debe ser un valor no negativo.")]
        public int? MarcadorB { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Los penales del equipo local deben ser un valor no negativo.")]
        public int? PenalesLocal { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Los penales del equipo visitante deben ser un valor no negativo.")]
        public int? PenalesVisitante { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Los penales del equipo A deben ser un valor no negativo.")]
        public int? PenalesEquipoA { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Los penales del equipo B deben ser un valor no negativo.")]
        public int? PenalesEquipoB { get; set; }

        public int? ObtenerMarcadorLocal() => MarcadorLocal ?? MarcadorA;
        public int? ObtenerMarcadorVisitante() => MarcadorVisitante ?? MarcadorB;
        public int? ObtenerPenalesLocal() => PenalesLocal ?? PenalesEquipoA;
        public int? ObtenerPenalesVisitante() => PenalesVisitante ?? PenalesEquipoB;
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
