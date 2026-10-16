using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class GenerarFixtureRequestDTO : IValidatableObject
    {
        public DateTime? FechaInicio { get; set; }
        public List<string> Horarios { get; set; } = new();
        public List<string> Sedes { get; set; } = new();

        public List<string>? FranjasHorarias { get; set; }
        public List<string>? CanchasDisponibles { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!FechaInicio.HasValue)
            {
                yield return new ValidationResult("La fecha de inicio es obligatoria.", new[] { nameof(FechaInicio) });
            }

            var tieneHorarios = (Horarios != null && Horarios.Count > 0) || (FranjasHorarias != null && FranjasHorarias.Count > 0);
            if (!tieneHorarios)
            {
                yield return new ValidationResult("Debe especificar al menos un horario o franja horaria.", new[] { nameof(Horarios) });
            }

            var tieneSedes = (Sedes != null && Sedes.Count > 0) || (CanchasDisponibles != null && CanchasDisponibles.Count > 0);
            if (!tieneSedes)
            {
                yield return new ValidationResult("Debe especificar al menos una sede o cancha disponible.", new[] { nameof(Sedes) });
            }
        }
    }

    public class PartidoResponseDTO
    {
        public Guid Id { get; set; }
        public Guid TorneoId { get; set; }
        public Guid? EquipoLocalId { get; set; }
        public string EquipoLocalNombre { get; set; } = string.Empty;
        public Guid? EquipoVisitanteId { get; set; }
        public string EquipoVisitanteNombre { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public string Sede { get; set; } = string.Empty;
        public int Ronda { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int? MarcadorLocal { get; set; }
        public int? MarcadorVisitante { get; set; }
        public int? PenalesLocal { get; set; }
        public int? PenalesVisitante { get; set; }
    }

    public class GenerarFixtureResponseDTO
    {
        public Guid TorneoId { get; set; }
        public string Mensaje { get; set; } = "Fixture generado exitosamente y fase de inscripciones cerrada.";
        public string EstadoTorneo { get; set; } = string.Empty;
        public int TotalPartidosGenerados { get; set; }
        public List<PartidoResponseDTO> Partidos { get; set; } = new();
    }

    public class FixtureParametrosDTO : IValidatableObject
    {
        public DateTime? FechaInicio { get; set; }
        public List<string> DiasJuego { get; set; } = new();
        public List<string> FranjasHorarias { get; set; } = new();
        public List<string> CanchasDisponibles { get; set; } = new();
        public int IntervaloMinutos { get; set; } = 60;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!FechaInicio.HasValue)
            {
                yield return new ValidationResult("La fecha de inicio es obligatoria.", new[] { nameof(FechaInicio) });
            }
            else if (FechaInicio.Value.Date < DateTime.Today)
            {
                yield return new ValidationResult("La fecha de inicio debe ser posterior o igual a la fecha actual", new[] { nameof(FechaInicio) });
            }

            if (FranjasHorarias == null || FranjasHorarias.Count == 0)
            {
                yield return new ValidationResult("Debe especificar al menos una franja horaria.", new[] { nameof(FranjasHorarias) });
            }

            if (CanchasDisponibles == null || CanchasDisponibles.Count == 0)
            {
                yield return new ValidationResult("Debe especificar al menos una cancha disponible.", new[] { nameof(CanchasDisponibles) });
            }
        }
    }

    public class FixtureResponseDTO
    {
        public Guid FixtureId { get; set; }
        public Guid TorneoId { get; set; }
        public string Mensaje { get; set; } = "Fixture generado exitosamente y torneo en desarrollo.";
        public string EstadoTorneo { get; set; } = string.Empty;
        public int TotalJornadas { get; set; }
        public int CantidadPartidosProgramados { get; set; }
        public int TotalPartidosProgramados { get; set; }
        public DateTime? FechaEstimadaFinalizacion { get; set; }
        public List<PartidoResponseDTO> Partidos { get; set; } = new();
        public List<string> Descansos { get; set; } = new();
        public List<string> PasesDirectos { get; set; } = new();
    }
}
