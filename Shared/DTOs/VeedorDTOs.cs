using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class VeedorCreateDTO
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        public string Dni { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato de correo electrónico es inválido.")]
        public string Email { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        public List<Guid>? TorneosAsignados { get; set; }
        public string? SedeAsignada { get; set; }
    }

    public class VeedorUpdateDTO
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        public string Apellido { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        public List<Guid>? TorneosAsignados { get; set; }
        public string? SedeAsignada { get; set; }
    }

    public class VeedorResponseDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string Rol { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public List<Guid> TorneosAsignados { get; set; } = new();
        public string? SedeAsignada { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class PartidoCronogramaResponseDTO
    {
        public Guid Id { get; set; }
        public Guid TorneoId { get; set; }
        public string TorneoNombre { get; set; } = string.Empty;
        public Guid? EquipoLocalId { get; set; }
        public string EquipoLocalNombre { get; set; } = string.Empty;
        public Guid? EquipoVisitanteId { get; set; }
        public string EquipoVisitanteNombre { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public string Sede { get; set; } = string.Empty;
        public int Ronda { get; set; }
        public string Estado { get; set; } = string.Empty;
        public Guid? VeedorId { get; set; }
    }
}
