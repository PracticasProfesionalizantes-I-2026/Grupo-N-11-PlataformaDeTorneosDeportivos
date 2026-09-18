using System;

namespace Shared.DTOs
{
    public class EquipoCreateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public Guid TorneoId { get; set; }
    }

    public class EquipoUpdateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Logo { get; set; }
    }

    public class EquipoResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string EstadoInscripcion { get; set; } = string.Empty;
        public Guid TorneoId { get; set; }
    }
}
