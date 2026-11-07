using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shared.DTOs
{
    public class GenerarFixtureRequestDTO : IValidatableObject
    {
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int? IntervaloMinutos { get; set; }
        public int? DuracionPartidoMinutos { get; set; }
        public string? HoraInicio { get; set; }
        public string? HoraFin { get; set; }
        public List<string> Horarios { get; set; } = new();
        public List<string> Sedes { get; set; } = new();

        public List<string>? FranjasHorarias { get; set; }
        public List<string>? CanchasDisponibles { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!FechaInicio.HasValue || FechaInicio.Value == default)
            {
                yield return new ValidationResult("La fecha de inicio es obligatoria.", new[] { nameof(FechaInicio) });
            }
            else if (FechaInicio.Value.Date < DateTime.Today || FechaInicio.Value.Year > 2100)
            {
                yield return new ValidationResult("La fecha de inicio debe ser una fecha válida a partir de hoy.", new[] { nameof(FechaInicio) });
            }

            if (FechaFin.HasValue)
            {
                if (FechaFin.Value == default)
                {
                    yield return new ValidationResult("La fecha de fin no puede ser una fecha por defecto.", new[] { nameof(FechaFin) });
                }
                else if (FechaInicio.HasValue && FechaFin.Value.Date < FechaInicio.Value.Date)
                {
                    yield return new ValidationResult("La fecha de fin no puede ser anterior a la fecha de inicio.", new[] { nameof(FechaFin) });
                }
                else if (FechaFin.Value.Year > 2100)
                {
                    yield return new ValidationResult("La fecha de fin excede el rango permitido.", new[] { nameof(FechaFin) });
                }
            }

            if (IntervaloMinutos.HasValue && (IntervaloMinutos.Value < 10 || IntervaloMinutos.Value > 240))
            {
                yield return new ValidationResult("El intervalo entre partidos debe estar entre 10 y 240 minutos.", new[] { nameof(IntervaloMinutos) });
            }

            if (DuracionPartidoMinutos.HasValue && (DuracionPartidoMinutos.Value < 10 || DuracionPartidoMinutos.Value > 240))
            {
                yield return new ValidationResult("La duración del partido debe estar entre 10 y 240 minutos.", new[] { nameof(DuracionPartidoMinutos) });
            }

            foreach (var err in FixtureValidationHelper.ValidateHoraInicioFinJornada(HoraInicio, HoraFin))
            {
                yield return err;
            }

            var horariosList = (Horarios != null && Horarios.Count > 0) ? Horarios : FranjasHorarias;
            if (horariosList == null || horariosList.Count == 0)
            {
                yield return new ValidationResult("Debe especificar al menos un horario o franja horaria.", new[] { nameof(Horarios) });
            }
            else
            {
                foreach (var error in FixtureValidationHelper.ValidateHorarios(horariosList, nameof(Horarios)))
                {
                    yield return error;
                }
            }

            var sedesList = (Sedes != null && Sedes.Count > 0) ? Sedes : CanchasDisponibles;
            if (sedesList == null || sedesList.Count == 0)
            {
                yield return new ValidationResult("Debe especificar al menos una sede o cancha disponible.", new[] { nameof(Sedes) });
            }
            else
            {
                foreach (var error in FixtureValidationHelper.ValidateCanchas(sedesList, nameof(Sedes)))
                {
                    yield return error;
                }
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
        public DateTime? FechaFin { get; set; }
        public List<string> DiasJuego { get; set; } = new();
        public List<string> FranjasHorarias { get; set; } = new();
        public List<string> CanchasDisponibles { get; set; } = new();
        public int IntervaloMinutos { get; set; } = 60;
        public int? DuracionPartidoMinutos { get; set; }
        public string? HoraInicio { get; set; }
        public string? HoraFin { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!FechaInicio.HasValue || FechaInicio.Value == default)
            {
                yield return new ValidationResult("La fecha de inicio es obligatoria.", new[] { nameof(FechaInicio) });
            }
            else if (FechaInicio.Value.Date < DateTime.Today || FechaInicio.Value.Year > 2100)
            {
                yield return new ValidationResult("La fecha de inicio debe ser posterior o igual a la fecha actual", new[] { nameof(FechaInicio) });
            }

            if (FechaFin.HasValue)
            {
                if (FechaFin.Value == default)
                {
                    yield return new ValidationResult("La fecha de fin no puede ser una fecha por defecto.", new[] { nameof(FechaFin) });
                }
                else if (FechaInicio.HasValue && FechaFin.Value.Date < FechaInicio.Value.Date)
                {
                    yield return new ValidationResult("La fecha de fin no puede ser anterior a la fecha de inicio.", new[] { nameof(FechaFin) });
                }
                else if (FechaFin.Value.Year > 2100)
                {
                    yield return new ValidationResult("La fecha de fin excede el rango permitido.", new[] { nameof(FechaFin) });
                }
            }

            if (IntervaloMinutos < 10 || IntervaloMinutos > 240)
            {
                yield return new ValidationResult("El intervalo entre partidos debe estar entre 10 y 240 minutos.", new[] { nameof(IntervaloMinutos) });
            }

            if (DuracionPartidoMinutos.HasValue && (DuracionPartidoMinutos.Value < 10 || DuracionPartidoMinutos.Value > 240))
            {
                yield return new ValidationResult("La duración del partido debe estar entre 10 y 240 minutos.", new[] { nameof(DuracionPartidoMinutos) });
            }

            foreach (var err in FixtureValidationHelper.ValidateHoraInicioFinJornada(HoraInicio, HoraFin))
            {
                yield return err;
            }

            if (DiasJuego != null && DiasJuego.Any(d => string.IsNullOrWhiteSpace(d) || d.Length > 30 || d.Contains('<') || d.Contains('>')))
            {
                yield return new ValidationResult("Los días de juego no pueden contener valores vacíos o inválidos.", new[] { nameof(DiasJuego) });
            }

            if (FranjasHorarias == null || FranjasHorarias.Count == 0)
            {
                yield return new ValidationResult("Debe especificar al menos una franja horaria.", new[] { nameof(FranjasHorarias) });
            }
            else
            {
                foreach (var error in FixtureValidationHelper.ValidateHorarios(FranjasHorarias, nameof(FranjasHorarias)))
                {
                    yield return error;
                }
            }

            if (CanchasDisponibles == null || CanchasDisponibles.Count == 0)
            {
                yield return new ValidationResult("Debe especificar al menos una cancha disponible.", new[] { nameof(CanchasDisponibles) });
            }
            else
            {
                foreach (var error in FixtureValidationHelper.ValidateCanchas(CanchasDisponibles, nameof(CanchasDisponibles)))
                {
                    yield return error;
                }
            }
        }
    }

    internal static class FixtureValidationHelper
    {
        private static readonly Regex SingleTimeRegex = new(@"^([01]?\d|2[0-3]):[0-5]\d(:[0-5]\d)?$", RegexOptions.Compiled);
        private static readonly Regex RangeTimeRegex = new(@"^([01]?\d|2[0-3]):[0-5]\d\s*-\s*([01]?\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

        public static IEnumerable<ValidationResult> ValidateHoraInicioFinJornada(string? horaInicio, string? horaFin)
        {
            TimeSpan? tsInicio = null;
            TimeSpan? tsFin = null;

            if (horaInicio != null)
            {
                var hIniTrim = horaInicio.Trim();
                if (string.IsNullOrWhiteSpace(hIniTrim) || !SingleTimeRegex.IsMatch(hIniTrim) || !TimeSpan.TryParse(hIniTrim, out var parsedIni))
                {
                    yield return new ValidationResult("La hora de inicio de jornada no tiene un formato válido (HH:mm).", new[] { "HoraInicio" });
                }
                else
                {
                    tsInicio = parsedIni;
                }
            }

            if (horaFin != null)
            {
                var hFinTrim = horaFin.Trim();
                if (string.IsNullOrWhiteSpace(hFinTrim) || !SingleTimeRegex.IsMatch(hFinTrim) || !TimeSpan.TryParse(hFinTrim, out var parsedFin))
                {
                    yield return new ValidationResult("La hora de fin de jornada no tiene un formato válido (HH:mm).", new[] { "HoraFin" });
                }
                else
                {
                    tsFin = parsedFin;
                }
            }

            if (tsInicio.HasValue && tsFin.HasValue && tsFin.Value <= tsInicio.Value)
            {
                yield return new ValidationResult("La hora de fin de jornada debe ser posterior a la hora de inicio.", new[] { "HoraFin" });
            }
        }

        public static IEnumerable<ValidationResult> ValidateHorarios(List<string> horarios, string memberName)
        {
            if (horarios.Count > 50)
            {
                yield return new ValidationResult("No se pueden especificar más de 50 franjas horarias.", new[] { memberName });
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in horarios)
            {
                if (string.IsNullOrWhiteSpace(h))
                {
                    yield return new ValidationResult("Los horarios no pueden estar vacíos ni contener solo espacios en blanco.", new[] { memberName });
                    yield break;
                }

                var trimmed = h.Trim();
                if (!seen.Add(trimmed))
                {
                    yield return new ValidationResult("No se permiten horarios o franjas horarias duplicadas.", new[] { memberName });
                    yield break;
                }

                if (RangeTimeRegex.IsMatch(trimmed))
                {
                    var parts = trimmed.Split('-', StringSplitOptions.TrimEntries);
                    if (!TimeSpan.TryParse(parts[0], out var inicio) ||
                        !TimeSpan.TryParse(parts[1], out var fin) ||
                        fin <= inicio)
                    {
                        yield return new ValidationResult("En una franja horaria, la hora de fin debe ser posterior a la hora de inicio.", new[] { memberName });
                        yield break;
                    }
                }
                else if (!SingleTimeRegex.IsMatch(trimmed))
                {
                    yield return new ValidationResult($"El horario '{trimmed}' no tiene un formato válido (HH:mm).", new[] { memberName });
                    yield break;
                }
            }
        }

        public static IEnumerable<ValidationResult> ValidateCanchas(List<string> canchas, string memberName)
        {
            if (canchas.Count > 50)
            {
                yield return new ValidationResult("No se pueden especificar más de 50 canchas o sedes.", new[] { memberName });
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in canchas)
            {
                if (string.IsNullOrWhiteSpace(c))
                {
                    yield return new ValidationResult("Las canchas o sedes no pueden estar vacías ni contener solo espacios en blanco.", new[] { memberName });
                    yield break;
                }

                var trimmed = c.Trim();
                if (trimmed.Length > 100 || trimmed.Contains('<') || trimmed.Contains('>'))
                {
                    yield return new ValidationResult("El nombre de la cancha o sede es inválido o excede los 100 caracteres.", new[] { memberName });
                    yield break;
                }

                if (!seen.Add(trimmed))
                {
                    yield return new ValidationResult("No se permiten canchas o sedes duplicadas.", new[] { memberName });
                    yield break;
                }
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
