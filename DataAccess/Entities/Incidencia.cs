using System;
using Shared.Enums;

namespace DataAccess.Entities
{
    public class Incidencia
    {
        public Guid Id { get; set; }

        public Guid PartidoId { get; set; }
        public Partido Partido { get; set; } = null!;

        public Guid EquipoId { get; set; }
        public Equipo Equipo { get; set; } = null!;

        public Guid JugadorId { get; set; }
        public Jugador Jugador { get; set; } = null!;

        public TipoIncidencia TipoIncidencia { get; set; }
        public int Minuto { get; set; }
        public string? Observaciones { get; set; }
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    }
}
