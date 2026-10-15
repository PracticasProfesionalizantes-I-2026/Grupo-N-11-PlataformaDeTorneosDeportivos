using System;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class RechazarInscripcionDTO
    {
        [Required(ErrorMessage = "El motivo de rechazo es obligatorio.")]
        [StringLength(500, ErrorMessage = "El motivo no puede exceder los 500 caracteres.")]
        public string MotivoRechazo { get; set; } = string.Empty;
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
