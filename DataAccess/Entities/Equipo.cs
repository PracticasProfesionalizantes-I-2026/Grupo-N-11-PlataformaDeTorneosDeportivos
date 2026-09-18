using System;
using System.Collections.Generic;
using Shared.Enums;

namespace DataAccess.Entities
{
    public class Equipo
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public EstadoInscripcion EstadoInscripcion { get; set; }

        public Guid TorneoId { get; set; }
        public Torneo Torneo { get; set; } = null!;

        public ICollection<Jugador> Jugadores { get; set; } = new List<Jugador>();
    }
}
