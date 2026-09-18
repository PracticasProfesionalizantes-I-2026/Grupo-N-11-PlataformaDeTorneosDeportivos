using System;
using Shared.Enums;

namespace Shared.DTOs
{
    public class TorneoCreateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public Disciplina Disciplina { get; set; }
        public Modalidad Modalidad { get; set; }
        public int CupoMaximo { get; set; }
        public decimal CostoInscripcion { get; set; }
    }

    public class TorneoUpdateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public Disciplina Disciplina { get; set; }
        public Modalidad Modalidad { get; set; }
        public int CupoMaximo { get; set; }
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
    }
}
