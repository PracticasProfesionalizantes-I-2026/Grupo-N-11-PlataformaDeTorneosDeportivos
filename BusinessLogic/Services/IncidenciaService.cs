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
    public class IncidenciaService : IIncidenciaService
    {
        private readonly IIncidenciaRepository _incidenciaRepository;
        private readonly IPartidoRepository _partidoRepository;
        private readonly IJugadorRepository _jugadorRepository;
        private readonly IEquipoRepository _equipoRepository;

        public IncidenciaService(
            IIncidenciaRepository incidenciaRepository,
            IPartidoRepository partidoRepository,
            IJugadorRepository jugadorRepository,
            IEquipoRepository equipoRepository)
        {
            _incidenciaRepository = incidenciaRepository;
            _partidoRepository = partidoRepository;
            _jugadorRepository = jugadorRepository;
            _equipoRepository = equipoRepository;
        }

        public async Task<IncidenciaResponseDTO> RegistrarIncidenciaAsync(Guid partidoId, IncidenciaCreateDTO dto)
        {
            if (partidoId == Guid.Empty)
            {
                throw new ValidationException("El identificador del partido no puede ser vacío.");
            }

            if (dto == null)
            {
                throw new ValidationException("Los datos de la incidencia son obligatorios.");
            }

            if (dto.Tipo.HasValue)
            {
                if (!Enum.IsDefined(typeof(TipoIncidencia), dto.Tipo.Value))
                {
                    throw new ValidationException("El tipo de incidencia especificado no es válido.");
                }
                dto.TipoIncidencia = dto.Tipo.Value;
            }

            if (!Enum.IsDefined(typeof(TipoIncidencia), dto.TipoIncidencia))
            {
                throw new ValidationException("El tipo de incidencia especificado no es válido.");
            }

            if (dto.Minuto < 1 || dto.Minuto > 150)
            {
                throw new ValidationException("El minuto de la incidencia debe estar entre 1 y 150.");
            }

            if (dto.JugadorId == Guid.Empty)
            {
                throw new ValidationException("El identificador del jugador no puede ser vacío.");
            }

            if (dto.EquipoId == Guid.Empty)
            {
                throw new ValidationException("El identificador del equipo no puede ser vacío.");
            }

            var rawObservaciones = dto.Observaciones ?? dto.Observacion ?? dto.Descripcion;
            if (rawObservaciones != null)
            {
                var obsTrim = rawObservaciones.Trim();
                if (string.IsNullOrWhiteSpace(obsTrim) || obsTrim.Length > 300 || obsTrim.Contains('<') || obsTrim.Contains('>'))
                {
                    throw new ValidationException("Las observaciones/descripción no pueden estar vacías, superar los 300 caracteres ni contener etiquetas HTML.");
                }
            }

            // 3a. Verificar existencia del partido
            var partido = await _partidoRepository.GetByIdWithTorneoAndEquiposAsync(partidoId);
            if (partido == null)
            {
                throw new PartidoNotFoundException("El partido especificado no existe");
            }

            // 3b. Verificar estado del partido
            if (partido.Estado == EstadoPartido.Finalizado || partido.Estado == EstadoPartido.Cancelado)
            {
                throw new PartidoEstadoInvalidoException("El partido no se encuentra habilitado para registrar incidencias en su estado actual");
            }

            // 3c. Verificar existencia y asignación del jugador a los equipos del partido
            var jugador = await _jugadorRepository.GetByIdAsync(dto.JugadorId);
            if (jugador == null)
            {
                throw new NotFoundException("El jugador especificado no existe");
            }

            bool esLocal = partido.EquipoLocalId.HasValue && jugador.EquipoId == partido.EquipoLocalId.Value;
            bool esVisitante = partido.EquipoVisitanteId.HasValue && jugador.EquipoId == partido.EquipoVisitanteId.Value;

            if (!esLocal && !esVisitante)
            {
                throw new JugadorNoAsignadoException("El jugador no pertenece a los planteles de este partido");
            }

            if (dto.EquipoId != jugador.EquipoId)
            {
                throw new JugadorNoAsignadoException("El jugador no pertenece al equipo indicado en este partido");
            }

            // 3e. RN-07: Verificar si el jugador ya se encuentra expulsado en este partido
            var incidenciasPrevias = (await _incidenciaRepository.GetByPartidoYJugadorAsync(partidoId, dto.JugadorId))?.ToList() ?? new List<Incidencia>();
            bool yaExpulsado = incidenciasPrevias.Any(i => i.TipoIncidencia == TipoIncidencia.TarjetaRoja) ||
                               incidenciasPrevias.Count(i => i.TipoIncidencia == TipoIncidencia.TarjetaAmarilla) >= 2;

            if (yaExpulsado)
            {
                throw new JugadorExpulsadoException("No es posible registrar incidencias para un jugador expulsado (RN-07)");
            }

            // 3d. Verificar inhabilitación disciplinaria previa (suspensión de fechas anteriores)
            if (jugador.Inhabilitado)
            {
                throw new JugadorInhabilitadoException("El jugador seleccionado se encuentra inhabilitado para este partido por sanción previa");
            }

            // RN-07: Evaluar si la nueva incidencia provoca expulsión
            bool quedaExpulsado = false;
            if (dto.TipoIncidencia == TipoIncidencia.TarjetaRoja)
            {
                quedaExpulsado = true;
            }
            else if (dto.TipoIncidencia == TipoIncidencia.TarjetaAmarilla)
            {
                int amarillasPrevias = incidenciasPrevias.Count(i => i.TipoIncidencia == TipoIncidencia.TarjetaAmarilla);
                if (amarillasPrevias >= 1)
                {
                    quedaExpulsado = true; // Segunda amarilla -> Expulsión inmediata
                }
            }

            if (quedaExpulsado)
            {
                jugador.Inhabilitado = true;
                await _jugadorRepository.UpdateAsync(jugador);
            }

            // Actualización de marcador parcial si corresponde a Gol o Punto
            if (dto.TipoIncidencia == TipoIncidencia.Gol || dto.TipoIncidencia == TipoIncidencia.Punto)
            {
                if (dto.EquipoId == partido.EquipoLocalId)
                {
                    partido.MarcadorLocal = (partido.MarcadorLocal ?? 0) + 1;
                }
                else if (dto.EquipoId == partido.EquipoVisitanteId)
                {
                    partido.MarcadorVisitante = (partido.MarcadorVisitante ?? 0) + 1;
                }
                await _partidoRepository.UpdateAsync(partido);
            }

            // Persistir la nueva incidencia
            var incidencia = new Incidencia
            {
                Id = Guid.NewGuid(),
                PartidoId = partidoId,
                EquipoId = dto.EquipoId,
                JugadorId = dto.JugadorId,
                TipoIncidencia = dto.TipoIncidencia,
                Minuto = dto.Minuto,
                Observaciones = rawObservaciones?.Trim(),
                FechaRegistro = DateTime.UtcNow
            };

            await _incidenciaRepository.AddAsync(incidencia);

            string equipoNombre = (dto.EquipoId == partido.EquipoLocalId)
                ? (partido.EquipoLocal?.Nombre ?? "Equipo Local")
                : (partido.EquipoVisitante?.Nombre ?? "Equipo Visitante");

            return MapToResponseDTO(incidencia, partido, jugador, equipoNombre, quedaExpulsado);
        }

        public async Task<IEnumerable<IncidenciaResponseDTO>> GetByPartidoIdAsync(Guid partidoId)
        {
            if (partidoId == Guid.Empty)
            {
                throw new ValidationException("El identificador del partido no puede ser vacío.");
            }

            var partido = await _partidoRepository.GetByIdWithTorneoAndEquiposAsync(partidoId)
                ?? await _partidoRepository.GetByIdAsync(partidoId);

            if (partido == null)
            {
                throw new PartidoNotFoundException("El partido especificado no existe");
            }

            var incidencias = (await _incidenciaRepository.GetByPartidoIdAsync(partidoId))?.ToList() ?? new List<Incidencia>();
            return incidencias.Select(i =>
            {
                string equipoNombre = i.Equipo?.Nombre
                    ?? (i.EquipoId == partido.EquipoLocalId
                        ? (partido.EquipoLocal?.Nombre ?? "Equipo Local")
                        : (partido.EquipoVisitante?.Nombre ?? "Equipo Visitante"));

                var jugador = i.Jugador ?? new Jugador { Id = i.JugadorId, Nombre = "Jugador", Apellido = string.Empty };
                bool expulsado = i.TipoIncidencia == TipoIncidencia.TarjetaRoja;
                return MapToResponseDTO(i, partido, jugador, equipoNombre, expulsado);
            }).ToList();
        }

        private static IncidenciaResponseDTO MapToResponseDTO(
            Incidencia incidencia,
            Partido partido,
            Jugador jugador,
            string equipoNombre,
            bool quedaExpulsado)
        {
            string estadoDisciplinario = quedaExpulsado ? "Expulsado" : "Habilitado";

            return new IncidenciaResponseDTO
            {
                Id = incidencia.Id,
                PartidoId = incidencia.PartidoId,
                EquipoId = incidencia.EquipoId,
                EquipoNombre = equipoNombre,
                JugadorId = incidencia.JugadorId,
                JugadorNombreCompleto = $"{jugador.Nombre} {jugador.Apellido}".Trim(),
                TipoIncidencia = incidencia.TipoIncidencia.ToString(),
                Minuto = incidencia.Minuto,
                Observaciones = incidencia.Observaciones,
                EstadoDisciplinarioJugador = estadoDisciplinario,
                JugadorExpulsado = quedaExpulsado,
                MarcadorLocalParcial = partido.MarcadorLocal ?? 0,
                MarcadorVisitanteParcial = partido.MarcadorVisitante ?? 0,
                FechaRegistro = incidencia.FechaRegistro,
                Mensaje = quedaExpulsado
                    ? "Incidencia registrada exitosamente. Jugador expulsado del encuentro (RN-07)."
                    : "Incidencia registrada exitosamente."
            };
        }
    }
}
