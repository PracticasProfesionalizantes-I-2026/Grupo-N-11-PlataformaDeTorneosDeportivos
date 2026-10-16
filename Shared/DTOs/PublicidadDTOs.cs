using System;

namespace Shared.DTOs
{
    public class PublicidadCreateDTO
    {
        public Guid? TorneoId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string NombrePatrocinador { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public string UrlDestino { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = "Superior";
        public bool Activo { get; set; } = true;
    }

    public class PublicidadResponseDTO
    {
        public Guid Id { get; set; }
        public Guid? TorneoId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string NombrePatrocinador { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public string UrlDestino { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public int Impresiones { get; set; }
        public int Clicks { get; set; }
    }

    public class PublicidadClickResponseDTO
    {
        public Guid BannerId { get; set; }
        public string UrlDestino { get; set; } = string.Empty;
        public int TotalClicks { get; set; }
        public string Mensaje { get; set; } = "Click registrado exitosamente.";
    }
}
