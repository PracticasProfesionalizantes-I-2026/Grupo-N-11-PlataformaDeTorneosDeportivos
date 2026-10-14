using System;
using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.DTOs
{
    public class TorneoCreateDTO
    {
        [Required(ErrorMessage = "El nombre del torneo es obligatorio.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public int? DisciplinaId { get; set; }

        public Disciplina? Disciplina { get; set; }

        [Required(ErrorMessage = "La modalidad es obligatoria.")]
        public Modalidad? Modalidad { get; set; }

        [Required(ErrorMessage = "El cupo máximo es obligatorio.")]
        [Range(1, int.MaxValue, ErrorMessage = "El cupo máximo debe ser mayor a cero.")]
        public int? CupoMaximo { get; set; }

        public bool EsPago { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El costo de inscripción no puede ser negativo.")]
        public decimal CostoInscripcion { get; set; }
    }

    public class TorneoUpdateDTO
    {
        [Required(ErrorMessage = "El nombre del torneo es obligatorio.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public Disciplina Disciplina { get; set; }
        public Modalidad Modalidad { get; set; }
        public int CupoMaximo { get; set; }
        public bool EsPago { get; set; }
        public decimal CostoInscripcion { get; set; }
    }

    public class TorneoResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Disciplina { get; set; } = string.Empty;
        public string Modalidad { get; set; } = string.Empty;
        public int CupoMaximo { get; set; }
        public decimal CostoInscripcion { get; set; }
        public bool EsPago { get; set; }
        public string Estado { get; set; } = "Abierto para Inscripción";
    }
}
