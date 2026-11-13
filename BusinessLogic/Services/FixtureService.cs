using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class FixtureService : IFixtureService
    {
        private readonly ITorneoRepository _torneoRepository;
        private readonly IPartidoRepository _partidoRepository;
        private readonly IUnitOfWork? _unitOfWork;

        public FixtureService(
            ITorneoRepository torneoRepository,
            IPartidoRepository partidoRepository,
            IUnitOfWork? unitOfWork = null)
        {
            _torneoRepository = torneoRepository;
            _partidoRepository = partidoRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<GenerarFixtureResponseDTO> GenerarFixtureAsync(Guid torneoId, GenerarFixtureRequestDTO dto)
        {
            return await GenerarFixtureUnificadoAsync(torneoId, dto);
        }

        public async Task<FixtureResponseDTO> GenerarFixtureAsync(Guid torneoId, FixtureParametrosDTO dto)
        {
            return await GenerarFixtureUnificadoAsync(torneoId, dto);
        }

        private async Task<FixtureResponseDTO> GenerarFixtureUnificadoAsync(Guid torneoId, GenerarFixtureRequestDTO dto)
        {
            if (torneoId == Guid.Empty)
            {
                throw new ValidationException("El identificador del torneo no puede ser vacío.");
            }

            if (dto == null)
            {
                throw new ValidationException("Los parámetros para generar el fixture son obligatorios.");
            }

            var validationErrors = dto.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(dto)).ToList();
            if (validationErrors.Count > 0)
            {
                throw new ValidationException(validationErrors[0].ErrorMessage ?? "Parámetros de fixture inválidos.");
            }

            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(torneoId) ?? await _torneoRepository.GetByIdAsync(torneoId);

            if (torneo == null)
            {
                throw new TorneoNotFoundException($"No se encontró el torneo con ID {torneoId}");
            }

            // Criterio 2.1: Validar estado estricto del torneo (InscripcionesCerradas)
            if (torneo.Estado != EstadoTorneo.InscripcionesCerradas)
            {
                throw new TorneoEstadoInvalidoException("El fixture solo puede generarse cuando el torneo se encuentra en estado Inscripciones Cerradas.");
            }

            if (torneo.Partidos != null && torneo.Partidos.Count > 0)
            {
                throw new TorneoEstadoInvalidoException("El torneo ya tiene un fixture generado.");
            }

            // Criterio 2.2: Validar cupo mínimo de equipos confirmados según modalidad (Liga >= 2, EliminacionDirecta >= 4)
            var equiposConfirmados = (torneo.Equipos ?? new List<Equipo>())
                .Where(e => e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado)
                .ToList();

            int cupoMinimo = torneo.Modalidad == Modalidad.Liga ? 2 : 4;
            if (equiposConfirmados.Count < cupoMinimo)
            {
                throw new TorneoCupoMinimoNoAlcanzadoException(
                    $"No se puede generar el fixture. Se requieren al menos {cupoMinimo} equipos confirmados para la modalidad {torneo.Modalidad} (mínimo de equipos no alcanzado).");
            }

            var rawHorarios = (dto.Horarios != null && dto.Horarios.Count > 0)
                ? dto.Horarios
                : (dto.FranjasHorarias ?? new List<string>());

            var rawSedes = (dto.Sedes != null && dto.Sedes.Count > 0)
                ? dto.Sedes
                : (dto.CanchasDisponibles ?? new List<string>());

            var franjas = rawHorarios.Select(f => f.Trim()).ToList();
            var canchas = rawSedes.Select(c => c.Trim()).ToList();
            int capacidadPorDia = canchas.Count * franjas.Count;

            if (capacidadPorDia <= 0)
            {
                throw new DisponibilidadHorariaInsuficienteException("Disponibilidad horaria insuficiente para programar los partidos respetando la RN-09. Amplíe días u horarios");
            }

            var fechaBase = dto.FechaInicio?.Date ?? DateTime.Today.AddDays(1);
            var partidosGenerados = new List<Partido>();
            var descansos = new List<string>();
            var pasesDirectos = new List<string>();
            int totalJornadas = 0;

            if (torneo.Modalidad == Modalidad.Liga)
            {
                // Validación RN-09: Un partido por día por equipo
                int effectiveTeams = equiposConfirmados.Count % 2 == 0 ? equiposConfirmados.Count : equiposConfirmados.Count + 1;
                int partidosPorRonda = effectiveTeams / 2;

                if (partidosPorRonda > capacidadPorDia)
                {
                    throw new DisponibilidadHorariaInsuficienteException("Disponibilidad horaria insuficiente para programar los partidos respetando la RN-09. Amplíe días u horarios");
                }

                var teams = new List<Equipo?>(equiposConfirmados);
                if (teams.Count % 2 != 0)
                {
                    teams.Add(null); // Bye
                }

                int n = teams.Count;
                totalJornadas = n - 1;

                for (int r = 0; r < totalJornadas; r++)
                {
                    var fechaRonda = fechaBase.AddDays(r);
                    int matchInRound = 0;

                    for (int i = 0; i < partidosPorRonda; i++)
                    {
                        var local = teams[i];
                        var visitante = teams[n - 1 - i];

                        if (local == null && visitante != null)
                        {
                            descansos.Add($"Jornada {r + 1}: Descansa {visitante.Nombre}");
                        }
                        else if (visitante == null && local != null)
                        {
                            descansos.Add($"Jornada {r + 1}: Descansa {local.Nombre}");
                        }
                        else if (local != null && visitante != null)
                        {
                            var cancha = canchas[matchInRound % canchas.Count];
                            var horario = franjas[matchInRound % franjas.Count];
                            var fechaHora = CombinarFechaYHora(fechaRonda, horario, matchInRound);

                            partidosGenerados.Add(new Partido
                            {
                                Id = Guid.NewGuid(),
                                TorneoId = torneo.Id,
                                EquipoLocalId = local.Id,
                                EquipoVisitanteId = visitante.Id,
                                FechaHora = fechaHora,
                                Sede = cancha,
                                Ronda = r + 1,
                                Estado = EstadoPartido.Pendiente
                            });

                            matchInRound++;
                        }
                    }

                    // Rotación estándar Round-Robin (fija el primero, rota el resto)
                    var ultimo = teams[n - 1];
                    teams.RemoveAt(n - 1);
                    teams.Insert(1, ultimo);
                }
            }
            else
            {
                // Eliminación Directa con cálculo de Byes hacia la siguiente potencia de 2
                int N = equiposConfirmados.Count;
                int p = NextPowerOfTwo(N);
                int byes = p - N;

                int crucesRonda1 = (N - byes) / 2;
                if (crucesRonda1 > capacidadPorDia)
                {
                    throw new DisponibilidadHorariaInsuficienteException("Disponibilidad horaria insuficiente para programar los partidos respetando la RN-09. Amplíe días u horarios");
                }

                for (int b = 0; b < byes; b++)
                {
                    pasesDirectos.Add(equiposConfirmados[b].Nombre);
                }

                var equiposRonda1 = equiposConfirmados.Skip(byes).ToList();
                int cruces = equiposRonda1.Count / 2;
                var fechaRonda = fechaBase;
                totalJornadas = (int)Math.Round(Math.Log2(p));

                for (int i = 0; i < cruces; i++)
                {
                    var local = equiposRonda1[i * 2];
                    var visitante = equiposRonda1[i * 2 + 1];

                    var cancha = canchas[i % canchas.Count];
                    var horario = franjas[i % franjas.Count];
                    var fechaHora = CombinarFechaYHora(fechaRonda, horario, i);

                    partidosGenerados.Add(new Partido
                    {
                        Id = Guid.NewGuid(),
                        TorneoId = torneo.Id,
                        EquipoLocalId = local.Id,
                        EquipoVisitanteId = visitante.Id,
                        FechaHora = fechaHora,
                        Sede = cancha,
                        Ronda = 1,
                        Estado = EstadoPartido.Pendiente
                    });
                }
            }

            // Criterio 2.3 y RN-08: Bloqueo permanente de inscripciones y pase a "En Desarrollo" bajo transacción ACID
            if (_unitOfWork != null)
            {
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    torneo.Estado = EstadoTorneo.EnDesarrollo;
                    await _torneoRepository.UpdateAsync(torneo);
                    await _partidoRepository.AddRangeAsync(partidosGenerados);
                    await _unitOfWork.CommitTransactionAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            else
            {
                torneo.Estado = EstadoTorneo.EnDesarrollo;
                await _torneoRepository.UpdateAsync(torneo);
                await _partidoRepository.AddRangeAsync(partidosGenerados);
            }

            var nombresEquipos = equiposConfirmados.ToDictionary(e => e.Id, e => e.Nombre);
            var fechaFin = partidosGenerados.Count > 0 ? partidosGenerados.Max(p => p.FechaHora) : fechaBase;

            return new FixtureResponseDTO
            {
                FixtureId = Guid.NewGuid(),
                TorneoId = torneo.Id,
                Mensaje = "Fixture generado exitosamente y torneo en desarrollo.",
                EstadoTorneo = torneo.Estado.ToString(),
                TotalJornadas = totalJornadas,
                CantidadPartidosProgramados = partidosGenerados.Count,
                TotalPartidosProgramados = partidosGenerados.Count,
                TotalPartidosGenerados = partidosGenerados.Count,
                FechaEstimadaFinalizacion = fechaFin,
                Partidos = partidosGenerados.Select(p => MapToPartidoResponseDTO(p, nombresEquipos)).ToList(),
                Descansos = descansos,
                PasesDirectos = pasesDirectos
            };
        }

        private static int NextPowerOfTwo(int n)
        {
            int p = 1;
            while (p < n) p *= 2;
            return p;
        }

        public async Task<IEnumerable<PartidoResponseDTO>> GetFixtureAsync(Guid torneoId)
        {
            if (torneoId == Guid.Empty)
            {
                throw new ValidationException("El identificador del torneo no puede ser vacío.");
            }

            var torneo = await _torneoRepository.GetByIdAsync(torneoId);
            if (torneo == null)
            {
                throw new NotFoundException($"No se encontró el torneo con ID {torneoId}");
            }

            var partidos = await _partidoRepository.GetPartidosByTorneoIdAsync(torneoId);
            return partidos.Select(MapToPartidoResponseDTO).ToList();
        }

        private static PartidoResponseDTO MapToPartidoResponseDTO(Partido p)
        {
            return new PartidoResponseDTO
            {
                Id = p.Id,
                TorneoId = p.TorneoId,
                EquipoLocalId = p.EquipoLocalId,
                EquipoLocalNombre = p.EquipoLocal?.Nombre ?? "Por Definir",
                EquipoVisitanteId = p.EquipoVisitanteId,
                EquipoVisitanteNombre = p.EquipoVisitante?.Nombre ?? "Por Definir",
                FechaHora = p.FechaHora,
                Sede = p.Sede,
                Ronda = p.Ronda,
                Estado = p.Estado.ToString(),
                MarcadorLocal = p.MarcadorLocal,
                MarcadorVisitante = p.MarcadorVisitante,
                PenalesLocal = p.PenalesLocal,
                PenalesVisitante = p.PenalesVisitante
            };
        }

        private static PartidoResponseDTO MapToPartidoResponseDTO(Partido p, Dictionary<Guid, string> nombresEquipos)
        {
            return new PartidoResponseDTO
            {
                Id = p.Id,
                TorneoId = p.TorneoId,
                EquipoLocalId = p.EquipoLocalId,
                EquipoLocalNombre = (p.EquipoLocalId.HasValue && nombresEquipos.TryGetValue(p.EquipoLocalId.Value, out var nLocal)) ? nLocal : "Equipo Local",
                EquipoVisitanteId = p.EquipoVisitanteId,
                EquipoVisitanteNombre = (p.EquipoVisitanteId.HasValue && nombresEquipos.TryGetValue(p.EquipoVisitanteId.Value, out var nVis)) ? nVis : "Equipo Visitante",
                FechaHora = p.FechaHora,
                Sede = p.Sede,
                Ronda = p.Ronda,
                Estado = p.Estado.ToString(),
                MarcadorLocal = p.MarcadorLocal,
                MarcadorVisitante = p.MarcadorVisitante,
                PenalesLocal = p.PenalesLocal,
                PenalesVisitante = p.PenalesVisitante
            };
        }

        private static DateTime CombinarFechaYHora(DateTime fecha, string horarioStr, int offsetIndice)
        {
            var cleanHorario = horarioStr?.Trim() ?? string.Empty;
            if (cleanHorario.Contains('-'))
            {
                cleanHorario = cleanHorario.Split('-')[0].Trim();
            }

            if (TimeSpan.TryParse(cleanHorario, out var ts) && ts >= TimeSpan.Zero && ts < TimeSpan.FromHours(24))
            {
                return fecha.Date.Add(ts);
            }
            return fecha.Date.AddHours(9).AddMinutes((offsetIndice % 12) * 60);
        }
    }
}
