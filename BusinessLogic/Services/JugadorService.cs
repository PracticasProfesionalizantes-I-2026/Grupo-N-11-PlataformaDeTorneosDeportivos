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
    public class JugadorService : IJugadorService
    {
        private readonly IJugadorRepository _jugadorRepository;
        private readonly IEquipoRepository _equipoRepository;
        private readonly IIncidenciaRepository? _incidenciaRepository;
        private readonly ITorneoRepository? _torneoRepository;

        public JugadorService(
            IJugadorRepository jugadorRepository, 
            IEquipoRepository equipoRepository,
            IIncidenciaRepository? incidenciaRepository = null,
            ITorneoRepository? torneoRepository = null)
        {
            _jugadorRepository = jugadorRepository;
            _equipoRepository = equipoRepository;
            _incidenciaRepository = incidenciaRepository;
            _torneoRepository = torneoRepository;
        }

        public async Task<IEnumerable<JugadorResponseDTO>> GetByEquipoIdAsync(Guid equipoId)
        {
            var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == equipoId);
            return jugadores.Select(MapToResponseDTO);
        }

        public async Task<JugadorResponseDTO> CreateAsync(JugadorCreateDTO dto)
        {
            var equipoDestino = await _equipoRepository.GetByIdAsync(dto.EquipoId);
            if (equipoDestino == null)
                throw new NotFoundException($"No se encontró el equipo con ID {dto.EquipoId}");

            // Regla Estricta: Un jugador no puede estar inscrito en más de un equipo en un mismo torneo.
            var jugadoresEnTorneo = await _jugadorRepository.FindAsync(j => j.Dni == dto.Dni);
            
            foreach(var jugadorExistente in jugadoresEnTorneo)
            {
                var equipoExistente = await _equipoRepository.GetByIdAsync(jugadorExistente.EquipoId);
                if (equipoExistente != null && equipoExistente.TorneoId == equipoDestino.TorneoId)
                {
                    throw new BusinessRuleConflictException($"El jugador con DNI {dto.Dni} ya se encuentra inscrito en el equipo '{equipoExistente.Nombre}' del mismo torneo.");
                }
            }

            var jugador = new Jugador
            {
                Nombre = dto.Nombre,
                Apellido = dto.Apellido,
                Dni = dto.Dni,
                EquipoId = dto.EquipoId
            };

            await _jugadorRepository.AddAsync(jugador);
            return MapToResponseDTO(jugador);
        }

        public async Task DeleteAsync(Guid id)
        {
            var jugador = await _jugadorRepository.GetByIdAsync(id);
            if (jugador == null)
                throw new NotFoundException($"No se encontró el jugador con ID {id}");

            await _jugadorRepository.DeleteAsync(jugador);
        }

        public async Task<JugadorEstadisticasDTO> GetEstadisticasAsync(Guid id)
        {
            var jugador = await _jugadorRepository.GetByIdWithEquipoAndTorneoAsync(id);
            if (jugador == null)
            {
                jugador = await _jugadorRepository.GetByIdAsync(id);
            }
            if (jugador == null)
            {
                throw new NotFoundException($"No se encontró el jugador con ID {id}");
            }

            Equipo? equipoActual = jugador.Equipo;
            if (equipoActual == null && jugador.EquipoId != Guid.Empty)
            {
                equipoActual = await _equipoRepository.GetByIdAsync(jugador.EquipoId);
            }

            // Recuperar todas las incidencias del jugador (por Id o por DNI)
            var incidencias = new List<Incidencia>();
            if (_incidenciaRepository != null)
            {
                var porId = await _incidenciaRepository.GetByJugadorIdAsync(id);
                incidencias.AddRange(porId);

                if (!string.IsNullOrWhiteSpace(jugador.Dni))
                {
                    var porDni = await _incidenciaRepository.GetByJugadorDniAsync(jugador.Dni);
                    foreach (var inc in porDni)
                    {
                        if (!incidencias.Any(x => x.Id == inc.Id))
                        {
                            incidencias.Add(inc);
                        }
                    }
                }
            }

            // Historial de torneos disputados
            var torneosDisputados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (equipoActual?.Torneo != null && !string.IsNullOrWhiteSpace(equipoActual.Torneo.Nombre))
            {
                torneosDisputados.Add(equipoActual.Torneo.Nombre);
            }
            else if (equipoActual != null && equipoActual.TorneoId != Guid.Empty && _torneoRepository != null)
            {
                var t = await _torneoRepository.GetByIdAsync(equipoActual.TorneoId);
                if (t != null && !string.IsNullOrWhiteSpace(t.Nombre))
                {
                    torneosDisputados.Add(t.Nombre);
                }
            }

            foreach (var inc in incidencias)
            {
                if (inc.Partido?.Torneo != null && !string.IsNullOrWhiteSpace(inc.Partido.Torneo.Nombre))
                {
                    torneosDisputados.Add(inc.Partido.Torneo.Nombre);
                }
            }

            if (!string.IsNullOrWhiteSpace(jugador.Dni))
            {
                var otrosRegistros = await _jugadorRepository.FindAsync(j => j.Dni == jugador.Dni);
                foreach (var reg in otrosRegistros)
                {
                    if (reg.Equipo?.Torneo != null && !string.IsNullOrWhiteSpace(reg.Equipo.Torneo.Nombre))
                    {
                        torneosDisputados.Add(reg.Equipo.Torneo.Nombre);
                    }
                    else if (reg.EquipoId != Guid.Empty)
                    {
                        var eq = await _equipoRepository.GetByIdAsync(reg.EquipoId);
                        if (eq != null && eq.TorneoId != Guid.Empty && _torneoRepository != null)
                        {
                            var t = await _torneoRepository.GetByIdAsync(eq.TorneoId);
                            if (t != null && !string.IsNullOrWhiteSpace(t.Nombre))
                            {
                                torneosDisputados.Add(t.Nombre);
                            }
                        }
                    }
                }
            }

            int goles = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.Gol);
            int puntos = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.Punto);
            int amarillas = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.TarjetaAmarilla);
            int rojas = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.TarjetaRoja);
            int faltas = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.Falta);

            return MapToEstadisticasDTO(jugador, equipoActual, goles, puntos, amarillas, rojas, faltas, torneosDisputados);
        }

        private static JugadorEstadisticasDTO MapToEstadisticasDTO(
            Jugador jugador,
            Equipo? equipoActual,
            int goles,
            int puntos,
            int amarillas,
            int rojas,
            int faltas,
            IEnumerable<string> torneosDisputados)
        {
            var listaTorneos = torneosDisputados.ToList();
            return new JugadorEstadisticasDTO
            {
                JugadorId = jugador.Id,
                Nombre = jugador.Nombre,
                Apellido = jugador.Apellido,
                Dni = jugador.Dni,
                EquipoActualId = equipoActual?.Id ?? (jugador.EquipoId != Guid.Empty ? jugador.EquipoId : null),
                EquipoActual = equipoActual?.Nombre ?? "Sin Equipo Asignado",
                Goles = goles,
                Puntos = puntos,
                TarjetasAmarillas = amarillas,
                TarjetasRojas = rojas,
                Faltas = faltas,
                TorneosDisputados = listaTorneos.Count,
                HistorialTorneos = listaTorneos
            };
        }

        private JugadorResponseDTO MapToResponseDTO(Jugador jugador)
        {
            return new JugadorResponseDTO
            {
                Id = jugador.Id,
                Nombre = jugador.Nombre,
                Apellido = jugador.Apellido,
                Dni = jugador.Dni,
                EquipoId = jugador.EquipoId
            };
        }
    }
}
