using System;
using System.Collections.Generic;

namespace Shared.DTOs
{
    public class PosicionTablaDTO
    {
        public int Posicion { get; set; }
        public Guid EquipoId { get; set; }
        public string EquipoNombre { get; set; } = string.Empty;
        public int PartidosJugados { get; set; }
        public int PartidosGanados { get; set; }
        public int PartidosEmpatados { get; set; }
        public int PartidosPerdidos { get; set; }
        public int GolesAFavor { get; set; }
        public int GolesEnContra { get; set; }
        public int DiferenciaGoles { get; set; }
        public int Puntos { get; set; }

        public int PJ => PartidosJugados;
        public int PG => PartidosGanados;
        public int PE => PartidosEmpatados;
        public int PP => PartidosPerdidos;
        public int GF => GolesAFavor;
        public int GC => GolesEnContra;
        public int Dif => DiferenciaGoles;
        public int Pts => Puntos;
    }

    public class BracketTorneoDTO
    {
        public Guid TorneoId { get; set; }
        public string TorneoNombre { get; set; } = string.Empty;
        public int TotalRondas { get; set; }
        public List<RondaBracketDTO> Rondas { get; set; } = new();
    }

    public class RondaBracketDTO
    {
        public int NumeroRonda { get; set; }
        public string NombreRonda { get; set; } = string.Empty;
        public List<PartidoBracketDTO> Partidos { get; set; } = new();
    }

    public class PartidoBracketDTO
    {
        public Guid Id { get; set; }
        public int Ronda { get; set; }
        public Guid? EquipoLocalId { get; set; }
        public string EquipoLocalNombre { get; set; } = "Por Definir";
        public Guid? EquipoVisitanteId { get; set; }
        public string EquipoVisitanteNombre { get; set; } = "Por Definir";
        public int? MarcadorLocal { get; set; }
        public int? MarcadorVisitante { get; set; }
        public int? PenalesLocal { get; set; }
        public int? PenalesVisitante { get; set; }
        public string Estado { get; set; } = string.Empty;
        public Guid? GanadorId { get; set; }
        public string? GanadorNombre { get; set; }
    }
}
