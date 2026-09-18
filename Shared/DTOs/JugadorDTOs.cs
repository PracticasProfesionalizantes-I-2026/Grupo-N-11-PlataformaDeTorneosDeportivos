using System;

namespace Shared.DTOs
{
    public class JugadorCreateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public Guid EquipoId { get; set; }
    }

    public class JugadorResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public Guid EquipoId { get; set; }
    }
}
