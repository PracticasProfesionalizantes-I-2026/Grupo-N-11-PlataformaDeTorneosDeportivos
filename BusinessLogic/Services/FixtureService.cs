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

        public FixtureService(ITorneoRepository torneoRepository, IPartidoRepository partidoRepository)
        {
            _torneoRepository = torneoRepository;
            _partidoRepository = partidoRepository;
        }

        public async Task<GenerarFixtureResponseDTO> GenerarFixtureAsync(Guid torneoId, GenerarFixtureRequestDTO dto)
        {
            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(torneoId);
            if (torneo == null)
            {
                // Fallback to GetByIdAsync if not found with includes
                torneo = await _torneoRepository.GetByIdAsync(torneoId);
            }

            if (torneo == null)
            {
                throw new NotFoundException($"No se encontró el torneo con ID {torneoId}");
            }

            // Validar estado del torneo: debe estar Abierto y sin fixture previo (Flujo 3b)
            if (torneo.Estado != EstadoTorneo.Abierto)
            {
                throw new TorneoEstadoInvalidoException("El torneo debe encontrarse en estado Abierto para generar el fixture.");
            }

            if (torneo.Partidos != null && torneo.Partidos.Count > 0)
            {
                throw new TorneoEstadoInvalidoException("El torneo ya tiene un fixture generado.");
            }

            // Validar cupo mínimo de equipos confirmados (Flujo 3a)
            var equiposConfirmados = torneo.Equipos
                .Where(e => e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado)
                .ToList();

            if (equiposConfirmados.Count < 4)
            {
                throw new TorneoCupoMinimoNoAlcanzadoException("No se puede generar el fixture. No se alcanza el mínimo de equipos requeridos.");
            }

            // Obtener listas de horarios y sedes
            var horarios = (dto.Horarios != null && dto.Horarios.Count > 0)
                ? dto.Horarios
                : (dto.FranjasHorarias != null && dto.FranjasHorarias.Count > 0 ? dto.FranjasHorarias : new List<string> { "10:00" });

            var sedes = (dto.Sedes != null && dto.Sedes.Count > 0)
                ? dto.Sedes
                : (dto.CanchasDisponibles != null && dto.CanchasDisponibles.Count > 0 ? dto.CanchasDisponibles : new List<string> { "Cancha 1" });

            var fechaBase = dto.FechaInicio ?? DateTime.Today.AddDays(1);
            var partidosGenerados = new List<Partido>();

            if (torneo.Modalidad == Modalidad.Liga)
            {
                // Round-Robin (Todos contra todos)
                var teams = new List<Equipo?>(equiposConfirmados);
                if (teams.Count % 2 != 0)
                {
                    teams.Add(null); // Bye
                }

                int n = teams.Count;
                int totalRondas = n - 1;
                int partidosPorRonda = n / 2;

                for (int r = 0; r < totalRondas; r++)
                {
                    // Regla RN-09: Un partido por día calendario por equipo (cada ronda en un día distinto)
                    var fechaRonda = fechaBase.Date.AddDays(r);
                    int partidoIndiceEnRonda = 0;

                    for (int i = 0; i < partidosPorRonda; i++)
                    {
                        var local = teams[i];
                        var visitante = teams[n - 1 - i];

                        if (local != null && visitante != null)
                        {
                            var horarioStr = horarios[partidoIndiceEnRonda % horarios.Count];
                            var sedeStr = sedes[partidoIndiceEnRonda % sedes.Count];
                            var fechaHora = CombinarFechaYHora(fechaRonda, horarioStr, partidoIndiceEnRonda);

                            partidosGenerados.Add(new Partido
                            {
                                Id = Guid.NewGuid(),
                                TorneoId = torneo.Id,
                                EquipoLocalId = local.Id,
                                EquipoVisitanteId = visitante.Id,
                                FechaHora = fechaHora,
                                Sede = sedeStr,
                                Ronda = r + 1,
                                Estado = EstadoPartido.Pendiente
                            });

                            partidoIndiceEnRonda++;
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
                // Eliminación Directa: cruces de primera ronda
                int cantidad = equiposConfirmados.Count;
                int cruces = cantidad / 2;
                var fechaRonda = fechaBase.Date;

                for (int i = 0; i < cruces; i++)
                {
                    var local = equiposConfirmados[i * 2];
                    var visitante = equiposConfirmados[i * 2 + 1];

                    var horarioStr = horarios[i % horarios.Count];
                    var sedeStr = sedes[i % sedes.Count];
                    var fechaHora = CombinarFechaYHora(fechaRonda, horarioStr, i);

                    partidosGenerados.Add(new Partido
                    {
                        Id = Guid.NewGuid(),
                        TorneoId = torneo.Id,
                        EquipoLocalId = local.Id,
                        EquipoVisitanteId = visitante.Id,
                        FechaHora = fechaHora,
                        Sede = sedeStr,
                        Ronda = 1,
                        Estado = EstadoPartido.Pendiente
                    });
                }
            }

            // RN-08: Cierre definitivo de inscripción del torneo y cambio de estado a "En Desarrollo"
            torneo.Estado = EstadoTorneo.EnDesarrollo;
            await _torneoRepository.UpdateAsync(torneo);
            await _partidoRepository.AddRangeAsync(partidosGenerados);

            var nombresEquipos = equiposConfirmados.ToDictionary(e => e.Id, e => e.Nombre);

            return MapToResponseDTO(torneo, partidosGenerados, nombresEquipos);
        }

        public async Task<FixtureResponseDTO> GenerarFixtureAsync(Guid torneoId, FixtureParametrosDTO dto)
        {
            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(torneoId) ?? await _torneoRepository.GetByIdAsync(torneoId);

            if (torneo == null)
            {
                throw new TorneoNotFoundException("El torneo especificado no existe");
            }

            // Validar estado del torneo: debe estar estrictamente en InscripcionesCerradas (Flujo 3b)
            if (torneo.Estado != EstadoTorneo.InscripcionesCerradas)
            {
                throw new TorneoEstadoInvalidoException("El fixture solo puede generarse cuando el torneo se encuentra en estado Inscripciones Cerradas");
            }

            if (torneo.Partidos != null && torneo.Partidos.Count > 0)
            {
                throw new TorneoEstadoInvalidoException("El torneo ya tiene un fixture generado.");
            }

            // Validar cupo mínimo de 4 equipos confirmados (Flujo 3c)
            var equiposConfirmados = (torneo.Equipos ?? new List<Equipo>())
                .Where(e => e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado)
                .ToList();

            if (equiposConfirmados.Count < 4)
            {
                throw new EquiposInsuficientesException("Se requieren al menos 4 equipos confirmados para generar el fixture");
            }

            var canchas = dto.CanchasDisponibles ?? new List<string>();
            var franjas = dto.FranjasHorarias ?? new List<string>();
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

                    // Rotación Round-Robin
                    var ultimo = teams[n - 1];
                    teams.RemoveAt(n - 1);
                    teams.Insert(1, ultimo);
                }
            }
            else
            {
                // Eliminación Directa con cálculo de Byes
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

            // RN-08: Bloqueo permanente de inscripciones y pase a "En Desarrollo"
            torneo.Estado = EstadoTorneo.EnDesarrollo;
            await _torneoRepository.UpdateAsync(torneo);
            await _partidoRepository.AddRangeAsync(partidosGenerados);

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
            var torneo = await _torneoRepository.GetByIdAsync(torneoId);
            if (torneo == null)
            {
                throw new NotFoundException($"No se encontró el torneo con ID {torneoId}");
            }

            var partidos = await _partidoRepository.GetPartidosByTorneoIdAsync(torneoId);
            return partidos.Select(MapToPartidoResponseDTO).ToList();
        }

        private static GenerarFixtureResponseDTO MapToResponseDTO(
            Torneo torneo, 
            List<Partido> partidos, 
            Dictionary<Guid, string> nombresEquipos)
        {
            return new GenerarFixtureResponseDTO
            {
                TorneoId = torneo.Id,
                Mensaje = "Fixture generado exitosamente y fase de inscripciones cerrada.",
                EstadoTorneo = torneo.Estado.ToString(),
                TotalPartidosGenerados = partidos.Count,
                Partidos = partidos.Select(p => MapToPartidoResponseDTO(p, nombresEquipos)).ToList()
            };
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
            if (TimeSpan.TryParse(horarioStr, out var ts))
            {
                return fecha.Date.Add(ts);
            }
            return fecha.Date.AddHours(9).AddMinutes(offsetIndice * 60);
        }
    }
}
