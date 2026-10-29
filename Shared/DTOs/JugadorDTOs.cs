using System;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class JugadorCreateDTO
    {
        [Required(ErrorMessage = "El nombre del jugador es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido del jugador es obligatorio.")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "El DNI debe contener solo números.")]
        public string Dni { get; set; } = string.Empty;

        public string? DocumentacionUrl { get; set; }

        public Guid EquipoId { get; set; }
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
