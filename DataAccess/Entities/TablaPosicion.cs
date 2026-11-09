using System;

namespace DataAccess.Entities
{
    public class TablaPosicion
    {
        public Guid Id { get; set; }
        public Guid TorneoId { get; set; }
        public Torneo Torneo { get; set; } = null!;
        public Guid EquipoId { get; set; }
        public Equipo Equipo { get; set; } = null!;
        public string EquipoNombre { get; set; } = string.Empty;
        public int Posicion { get; set; }
        public int PartidosJugados { get; set; }
        public int PartidosGanados { get; set; }
        public int PartidosEmpatados { get; set; }
        public int PartidosPerdidos { get; set; }
        public int GolesAFavor { get; set; }
        public int GolesEnContra { get; set; }
        public int DiferenciaGoles { get; set; }
        public int Puntos { get; set; }
        public DateTime UltimaActualizacion { get; set; } = DateTime.UtcNow;
    }
}

