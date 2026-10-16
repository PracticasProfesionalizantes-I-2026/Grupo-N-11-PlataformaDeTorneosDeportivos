using System;
using Shared.Enums;

namespace DataAccess.Entities
{
    public class Partido
    {
        public Guid Id { get; set; }
        public Guid TorneoId { get; set; }
        public Torneo Torneo { get; set; } = null!;

        public Guid? EquipoLocalId { get; set; }
        public Equipo? EquipoLocal { get; set; }

        public Guid? EquipoVisitanteId { get; set; }
        public Equipo? EquipoVisitante { get; set; }

        public Guid? VeedorId { get; set; }
        public Usuario? Veedor { get; set; }

        public DateTime FechaHora { get; set; }
        public string Sede { get; set; } = string.Empty;
        public int Ronda { get; set; }
        public EstadoPartido Estado { get; set; } = EstadoPartido.Pendiente;
    }
}
