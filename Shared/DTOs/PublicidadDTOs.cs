using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class PublicidadCreateDTO : IValidatableObject
    {
        public Guid? TorneoId { get; set; }

        [Required(ErrorMessage = "El título del banner es obligatorio.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El título debe tener entre 3 y 100 caracteres.")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del patrocinador es obligatorio.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre del patrocinador debe tener entre 2 y 100 caracteres.")]
        public string NombrePatrocinador { get; set; } = string.Empty;

        [Required(ErrorMessage = "La URL de la imagen es obligatoria.")]
        [StringLength(500, ErrorMessage = "La URL de la imagen no puede exceder los 500 caracteres.")]
        public string ImagenUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "La URL de destino es obligatoria.")]
        [StringLength(500, ErrorMessage = "La URL de destino no puede exceder los 500 caracteres.")]
        public string UrlDestino { get; set; } = string.Empty;

        [StringLength(50, MinimumLength = 2, ErrorMessage = "La ubicación debe tener entre 2 y 50 caracteres.")]
        public string Ubicacion { get; set; } = "Superior";

        public bool Activo { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (TorneoId.HasValue && TorneoId.Value == Guid.Empty)
            {
                yield return new ValidationResult("El identificador del torneo no puede ser vacío.", new[] { nameof(TorneoId) });
            }

            var tituloTrim = Titulo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tituloTrim) || tituloTrim.Length < 3 || tituloTrim.Length > 100 || tituloTrim.Contains('<') || tituloTrim.Contains('>'))
            {
                yield return new ValidationResult("El título del banner debe tener entre 3 y 100 caracteres válidos.", new[] { nameof(Titulo) });
            }

            var patrocinadorTrim = NombrePatrocinador?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(patrocinadorTrim) || patrocinadorTrim.Length < 2 || patrocinadorTrim.Length > 100 || patrocinadorTrim.Contains('<') || patrocinadorTrim.Contains('>'))
            {
                yield return new ValidationResult("El nombre del patrocinador debe tener entre 2 y 100 caracteres válidos.", new[] { nameof(NombrePatrocinador) });
            }

            var imagenTrim = ImagenUrl?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(imagenTrim) || imagenTrim.Length > 500 || imagenTrim.Contains('<') || imagenTrim.Contains('>') ||
                !Uri.TryCreate(imagenTrim, UriKind.Absolute, out var imgUri) || (imgUri.Scheme != Uri.UriSchemeHttp && imgUri.Scheme != Uri.UriSchemeHttps))
            {
                yield return new ValidationResult("La URL de la imagen debe tener un formato de URL válido (http/https) y no superar los 500 caracteres.", new[] { nameof(ImagenUrl) });
            }

            var destinoTrim = UrlDestino?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(destinoTrim) || destinoTrim.Length > 500 || destinoTrim.Contains('<') || destinoTrim.Contains('>') ||
                !Uri.TryCreate(destinoTrim, UriKind.Absolute, out var destUri) || (destUri.Scheme != Uri.UriSchemeHttp && destUri.Scheme != Uri.UriSchemeHttps))
            {
                yield return new ValidationResult("La URL de destino debe tener un formato de URL válido (http/https) y no superar los 500 caracteres.", new[] { nameof(UrlDestino) });
            }

            var ubicacionTrim = Ubicacion?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ubicacionTrim) || ubicacionTrim.Length < 2 || ubicacionTrim.Length > 50 || ubicacionTrim.Contains('<') || ubicacionTrim.Contains('>'))
            {
                yield return new ValidationResult("La ubicación del banner debe tener entre 2 y 50 caracteres válidos.", new[] { nameof(Ubicacion) });
            }
        }
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
