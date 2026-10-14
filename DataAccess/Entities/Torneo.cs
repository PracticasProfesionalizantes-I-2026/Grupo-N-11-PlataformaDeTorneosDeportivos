using System;
using System.Collections.Generic;
using Shared.Enums;

namespace DataAccess.Entities
{
    public class Torneo
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public Disciplina Disciplina { get; set; }
        public Modalidad Modalidad { get; set; }
        public int CupoMaximo { get; set; }
        public bool EsPago { get; set; }
        public decimal CostoInscripcion { get; set; }
        public string Estado { get; set; } = "Abierto para Inscripción";

        public ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();
    }
}
