using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class EquipoCreateDTO
    {
        [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }
        public string? Logo { get => LogoUrl; set => LogoUrl = value; }

        public Guid TorneoId { get; set; }

        [Required(ErrorMessage = "La lista de jugadores es obligatoria.")]
        public List<JugadorCreateDTO> Jugadores { get; set; } = new();
    }

    public class EquipoUpdateDTO
    {
        [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;
        public string? Logo { get; set; }
    }

    public class EquipoResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string? LogoUrl { get => Logo; set => Logo = value; }
        public string EstadoInscripcion { get; set; } = string.Empty;
        public Guid TorneoId { get; set; }
        public int CantidadJugadores { get; set; }
        public List<JugadorResponseDTO> Jugadores { get; set; } = new();
    }
}
