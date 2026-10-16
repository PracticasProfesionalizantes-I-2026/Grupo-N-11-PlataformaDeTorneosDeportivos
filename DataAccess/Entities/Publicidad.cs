using System;

namespace DataAccess.Entities
{
    public class Publicidad
    {
        public Guid Id { get; set; }
        public Guid? TorneoId { get; set; }
        public Torneo? Torneo { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string NombrePatrocinador { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public string UrlDestino { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = "Superior";
        public bool Activo { get; set; } = true;
        public int Impresiones { get; set; } = 0;
        public int Clicks { get; set; } = 0;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
