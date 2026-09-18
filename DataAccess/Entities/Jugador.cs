using System;

namespace DataAccess.Entities
{
    public class Jugador
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;

        public Guid EquipoId { get; set; }
        public Equipo Equipo { get; set; } = null!;
    }
}
