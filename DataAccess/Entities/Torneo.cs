using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        public decimal CostoInscripcion { get; set; }
        public bool EsPago { get; set; }
        public EstadoTorneo Estado { get; set; } = EstadoTorneo.Abierto;

        [Timestamp]
        [ConcurrencyCheck]
        public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();

        public Guid? CampeonId { get; set; }
        public Equipo? Campeon { get; set; }

        public ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();
        public ICollection<Partido> Partidos { get; set; } = new List<Partido>();
    }
}
