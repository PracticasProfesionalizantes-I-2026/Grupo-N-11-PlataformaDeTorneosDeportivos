using System;
using System.Collections.Generic;

namespace Shared.DTOs
{
    public class JugadorEstadisticasDTO
    {
        public Guid JugadorId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string NombreCompleto => $"{Nombre} {Apellido}".Trim();
        public string Dni { get; set; } = string.Empty;
        public Guid? EquipoActualId { get; set; }
        public string EquipoActual { get; set; } = string.Empty;

        // Métricas individuales
        public int Goles { get; set; }
        public int Puntos { get; set; }
        public int TotalGolesOPuntos => Goles + Puntos;
        public int TarjetasAmarillas { get; set; }
        public int TarjetasRojas { get; set; }
        public int Faltas { get; set; }

        // Historial y torneos
        public int TorneosDisputados { get; set; }
        public List<string> HistorialTorneos { get; set; } = new();
    }

    public class EquipoEstadisticasDTO
    {
        public Guid EquipoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string? LogoUrl => Logo;
        public Guid TorneoActualId { get; set; }
        public string TorneoActual { get; set; } = string.Empty;

        // Métricas colectivas
        public int PartidosJugados { get; set; }
        public int Victorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int GolesAFavor { get; set; }
        public int GolesEnContra { get; set; }
        public int DiferenciaGoles { get; set; }
        public double Efectividad { get; set; }
        public double PorcentajeVictorias { get; set; }

        // Historial y plantel
        public int TorneosDisputados { get; set; }
        public List<string> HistorialTorneos { get; set; } = new();
        public List<JugadorResponseDTO> Integrantes { get; set; } = new();
    }
}
