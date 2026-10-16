using System;
using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.DTOs
{
    public class IncidenciaCreateDTO
    {
        [Required(ErrorMessage = "El tipo de incidencia es obligatorio.")]
        public TipoIncidencia TipoIncidencia { get; set; }

        [Range(0, 150, ErrorMessage = "El minuto debe encontrarse entre 0 y 150.")]
        public int Minuto { get; set; }

        [Required(ErrorMessage = "El identificador del jugador es obligatorio.")]
        public Guid JugadorId { get; set; }

        [Required(ErrorMessage = "El identificador del equipo es obligatorio.")]
        public Guid EquipoId { get; set; }

        [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
        public string? Observaciones { get; set; }
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
