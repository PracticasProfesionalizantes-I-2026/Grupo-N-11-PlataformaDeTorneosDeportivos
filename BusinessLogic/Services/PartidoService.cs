using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Common;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class PartidoService : IPartidoService
    {
        private readonly IPartidoRepository _partidoRepository;
        private readonly ITorneoRepository? _torneoRepository;
        private readonly IUnitOfWork? _unitOfWork;
        private readonly ITablaPosicionRepository? _tablaPosicionRepository;

        public PartidoService(
            IPartidoRepository partidoRepository,
            ITorneoRepository? torneoRepository = null,
            IUnitOfWork? unitOfWork = null,
            ITablaPosicionRepository? tablaPosicionRepository = null)
        {
            _partidoRepository = partidoRepository;
            _torneoRepository = torneoRepository;
            _unitOfWork = unitOfWork;
            _tablaPosicionRepository = tablaPosicionRepository;
        }

        public async Task<PartidoResultadoResponseDTO> LoadResultadoAsync(Guid id, CargarResultadoDTO dto)
        {
            if (id == Guid.Empty)
            {
                throw new ValidationException("El identificador del partido no puede ser vacío.");
            }

            if (dto == null)
            {
                throw new ValidationException("Los datos del resultado son obligatorios.");
            }

            var marcadorLocal = dto.ObtenerMarcadorLocal();
            var marcadorVisitante = dto.ObtenerMarcadorVisitante();

            if (!marcadorLocal.HasValue || !marcadorVisitante.HasValue)
            {
                throw new ValidationException("Debe ingresar los marcadores de ambos equipos.");
            }

            if (marcadorLocal.Value < 0 || marcadorVisitante.Value < 0)
            {
                throw new ValidationException("Los marcadores deben ser valores numéricos no negativos.");
            }

            if (marcadorLocal.Value > 999 || marcadorVisitante.Value > 999)
            {
                throw new ValidationException("Los marcadores deben estar entre 0 y 999.");
            }

            var penalesLocal = dto.ObtenerPenalesLocal();
            var penalesVisitante = dto.ObtenerPenalesVisitante();

            if (penalesLocal.HasValue != penalesVisitante.HasValue)
            {
                throw new ValidationException("Si se informan penales, deben especificarse para ambos equipos.");
            }

            if ((penalesLocal.HasValue && penalesLocal.Value < 0) || 
                (penalesVisitante.HasValue && penalesVisitante.Value < 0))
            {
                throw new ValidationException("Los penales deben ser valores no negativos.");
            }

            if ((penalesLocal.HasValue && penalesLocal.Value > 999) || 
                (penalesVisitante.HasValue && penalesVisitante.Value > 999))
            {
                throw new ValidationException("Los penales deben estar entre 0 y 999.");
            }

            var partido = await _partidoRepository.GetByIdWithTorneoAndEquiposAsync(id);
            if (partido == null)
            {
                throw new NotFoundException($"No se encontró el partido con ID {id}");
            }

            // Flujo Alternativo 3a: Partido no apto para recibir resultados (409 Conflict)
            if (partido.Estado == EstadoPartido.Finalizado || partido.Estado == EstadoPartido.Cancelado)
            {
                throw new PartidoEstadoInvalidoException("El partido no admite carga de resultados en su estado actual.");
            }

            // Flujo Alternativo 3b: Empate no permitido en Eliminación Directa (409 Conflict)
            if (partido.Torneo != null && partido.Torneo.Modalidad == Modalidad.EliminacionDirecta)
            {
                if (marcadorLocal.Value == marcadorVisitante.Value)
                {
                    if (!penalesLocal.HasValue || !penalesVisitante.HasValue || penalesLocal.Value == penalesVisitante.Value)
                    {
                        throw new EmpateNoPermitidoException("Esta modalidad no admite empates. Debe definir el ganador (Penales/Tiempo Extra)");
                    }
                }
            }

            if (_unitOfWork != null)
            {
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    await EjecutarCargaResultadoYAvanceAsync(partido, marcadorLocal.Value, marcadorVisitante.Value, penalesLocal, penalesVisitante);
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
                await EjecutarCargaResultadoYAvanceAsync(partido, marcadorLocal.Value, marcadorVisitante.Value, penalesLocal, penalesVisitante);
            }

            return MapToResponseDTO(partido);
        }

        private async Task EjecutarCargaResultadoYAvanceAsync(
            Partido partido,
            int marcadorLocal,
            int marcadorVisitante,
            int? penalesLocal,
            int? penalesVisitante)
        {
            // Flujo Principal: Actualizar marcador y pasar a Finalizado
            partido.MarcadorLocal = marcadorLocal;
            partido.MarcadorVisitante = marcadorVisitante;
            partido.PenalesLocal = penalesLocal;
            partido.PenalesVisitante = penalesVisitante;
            partido.Estado = EstadoPartido.Finalizado;

            await _partidoRepository.UpdateAsync(partido);

            // Obtener torneo
            var torneo = partido.Torneo ?? (_torneoRepository != null ? await _torneoRepository.GetByIdAsync(partido.TorneoId) : null);
            if (torneo == null)
            {
                return;
            }

            // Si es torneo de Liga, actualizar y persistir la tabla de posiciones inmediatamente
            if (torneo.Modalidad == Modalidad.Liga && _tablaPosicionRepository != null)
            {
                await ActualizarTablaPosicionesLigaAsync(torneo.Id);
                return;
            }

            if (torneo.Modalidad != Modalidad.EliminacionDirecta)
            {
                return;
            }

            var ganadorActual = ObtenerGanador(partido);
            if (!ganadorActual.HasValue)
            {
                return;
            }

            // Consultar todos los partidos del torneo para analizar la ronda actual y las llaves
            var partidosQuery = await _partidoRepository.GetPartidosByTorneoIdAsync(partido.TorneoId);
            var todosPartidos = partidosQuery != null ? partidosQuery.ToList() : new List<Partido>();

            var idx = todosPartidos.FindIndex(p => p.Id == partido.Id);
            if (idx >= 0)
            {
                todosPartidos[idx] = partido;
            }
            else
            {
                todosPartidos.Add(partido);
            }

            var partidosRondaActual = todosPartidos
                .Where(p => p.Ronda == partido.Ronda)
                .OrderBy(p => p.FechaHora)
                .ThenBy(p => p.Id)
                .ToList();

            // CASO A: Es el Partido Final (solo existe 1 partido en la ronda actual)
            if (partidosRondaActual.Count == 1)
            {
                torneo.CampeonId = ganadorActual.Value;
                torneo.Estado = EstadoTorneo.Finalizado;

                if (_torneoRepository != null)
                {
                    await _torneoRepository.UpdateAsync(torneo);
                }
                return;
            }

            // CASO B: Es una Ronda Previa (Cruces de llaves eliminatorias)
            int cruces = partidosRondaActual.Count / 2;
            for (int k = 0; k < cruces; k++)
            {
                var matchPar = partidosRondaActual[2 * k];
                var matchImpar = partidosRondaActual[2 * k + 1];

                if (matchPar.Id == partido.Id || matchImpar.Id == partido.Id)
                {
                    // Solo avanzar si ambos partidos del cruce han finalizado
                    if (matchPar.Estado == EstadoPartido.Finalizado && matchImpar.Estado == EstadoPartido.Finalizado)
                    {
                        var ganadorPar = ObtenerGanador(matchPar);
                        var ganadorImpar = ObtenerGanador(matchImpar);

                        if (ganadorPar.HasValue && ganadorImpar.HasValue)
                        {
                            int rondaSiguiente = partido.Ronda + 1;
                            var partidosRondaSiguiente = todosPartidos
                                .Where(p => p.Ronda == rondaSiguiente)
                                .OrderBy(p => p.FechaHora)
                                .ThenBy(p => p.Id)
                                .ToList();

                            // Buscar si ya existe el partido programado para este cruce
                            var matchSiguiente = partidosRondaSiguiente.FirstOrDefault(p =>
                                (p.FechaHora.Hour == 10 + k * 2) ||
                                (p.EquipoLocalId == ganadorPar.Value && p.EquipoVisitanteId == ganadorImpar.Value));

                            if (matchSiguiente != null)
                            {
                                matchSiguiente.EquipoLocalId = ganadorPar.Value;
                                matchSiguiente.EquipoVisitanteId = ganadorImpar.Value;
                                matchSiguiente.Estado = EstadoPartido.Pendiente;

                                await _partidoRepository.UpdateAsync(matchSiguiente);
                            }
                            else
                            {
                                var maxFecha = matchPar.FechaHora > matchImpar.FechaHora ? matchPar.FechaHora : matchImpar.FechaHora;
                                var fechaSiguiente = maxFecha.Date.AddDays(1).AddHours(10 + k * 2);

                                var nuevoPartido = new Partido
                                {
                                    Id = Guid.NewGuid(),
                                    TorneoId = torneo.Id,
                                    EquipoLocalId = ganadorPar.Value,
                                    EquipoVisitanteId = ganadorImpar.Value,
                                    Ronda = rondaSiguiente,
                                    Estado = EstadoPartido.Pendiente,
                                    FechaHora = fechaSiguiente,
                                    Sede = matchPar.Sede ?? "Cancha 1"
                                };

                                await _partidoRepository.AddAsync(nuevoPartido);
                            }
                        }
                    }
                    break;
                }
            }
        }

        private static Guid? ObtenerGanador(Partido partido)
        {
            if (!partido.MarcadorLocal.HasValue || !partido.MarcadorVisitante.HasValue)
                return null;

            if (partido.MarcadorLocal.Value > partido.MarcadorVisitante.Value)
                return partido.EquipoLocalId;

            if (partido.MarcadorVisitante.Value > partido.MarcadorLocal.Value)
                return partido.EquipoVisitanteId;

            if (partido.PenalesLocal.HasValue && partido.PenalesVisitante.HasValue)
            {
                if (partido.PenalesLocal.Value > partido.PenalesVisitante.Value)
                    return partido.EquipoLocalId;
                if (partido.PenalesVisitante.Value > partido.PenalesLocal.Value)
                    return partido.EquipoVisitanteId;
            }

            return null;
        }

        private static PartidoResultadoResponseDTO MapToResponseDTO(Partido partido)
        {
            Guid? ganadorId = null;
            string? ganadorNombre = null;

            if (partido.MarcadorLocal.HasValue && partido.MarcadorVisitante.HasValue)
            {
                if (partido.MarcadorLocal.Value > partido.MarcadorVisitante.Value)
                {
                    ganadorId = partido.EquipoLocalId;
                    ganadorNombre = partido.EquipoLocal?.Nombre;
                }
                else if (partido.MarcadorVisitante.Value > partido.MarcadorLocal.Value)
                {
                    ganadorId = partido.EquipoVisitanteId;
                    ganadorNombre = partido.EquipoVisitante?.Nombre;
                }
                else if (partido.PenalesLocal.HasValue && partido.PenalesVisitante.HasValue)
                {
                    if (partido.PenalesLocal.Value > partido.PenalesVisitante.Value)
                    {
                        ganadorId = partido.EquipoLocalId;
                        ganadorNombre = partido.EquipoLocal?.Nombre;
                    }
                    else if (partido.PenalesVisitante.Value > partido.PenalesLocal.Value)
                    {
                        ganadorId = partido.EquipoVisitanteId;
                        ganadorNombre = partido.EquipoVisitante?.Nombre;
                    }
                }
            }

            return new PartidoResultadoResponseDTO
            {
                Id = partido.Id,
                TorneoId = partido.TorneoId,
                TorneoNombre = partido.Torneo?.Nombre ?? string.Empty,
                EquipoLocalId = partido.EquipoLocalId,
                EquipoLocalNombre = partido.EquipoLocal?.Nombre ?? "Equipo Local",
                EquipoVisitanteId = partido.EquipoVisitanteId,
                EquipoVisitanteNombre = partido.EquipoVisitante?.Nombre ?? "Equipo Visitante",
                MarcadorLocal = partido.MarcadorLocal ?? 0,
                MarcadorVisitante = partido.MarcadorVisitante ?? 0,
                PenalesLocal = partido.PenalesLocal,
                PenalesVisitante = partido.PenalesVisitante,
                Estado = partido.Estado.ToString(),
                GanadorId = ganadorId,
                GanadorNombre = ganadorNombre,
                Mensaje = "Marcador y estado del partido guardados exitosamente."
            };
        }

        private async Task ActualizarTablaPosicionesLigaAsync(Guid torneoId)
        {
            if (_tablaPosicionRepository == null) return;

            var torneoConEquipos = _torneoRepository != null
                ? await _torneoRepository.GetTorneoConEquiposAsync(torneoId)
                : null;

            var equipos = torneoConEquipos?.Equipos;
            var partidos = await _partidoRepository.GetPartidosByTorneoIdAsync(torneoId);

            var entidades = StandingsCalculator.CalculateStandingsEntities(torneoId, equipos, partidos);
            if (entidades.Count > 0)
            {
                await _tablaPosicionRepository.UpsertPosicionesTorneoAsync(torneoId, entidades);
            }
        }
    }
}
