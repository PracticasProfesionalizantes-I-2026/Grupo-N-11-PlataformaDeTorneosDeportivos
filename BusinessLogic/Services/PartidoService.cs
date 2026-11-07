using System;
using System.Threading.Tasks;
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

        public PartidoService(IPartidoRepository partidoRepository)
        {
            _partidoRepository = partidoRepository;
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

            // Flujo Principal: Actualizar marcador y pasar a Finalizado
            partido.MarcadorLocal = marcadorLocal.Value;
            partido.MarcadorVisitante = marcadorVisitante.Value;
            partido.PenalesLocal = penalesLocal;
            partido.PenalesVisitante = penalesVisitante;
            partido.Estado = EstadoPartido.Finalizado;

            await _partidoRepository.UpdateAsync(partido);

            return MapToResponseDTO(partido);
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
    }
}
