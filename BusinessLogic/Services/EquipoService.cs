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
    public class EquipoService : IEquipoService
    {
        private readonly IEquipoRepository _equipoRepository;
        private readonly ITorneoRepository _torneoRepository;
        private readonly IJugadorRepository _jugadorRepository;

        public EquipoService(IEquipoRepository equipoRepository, ITorneoRepository torneoRepository, IJugadorRepository jugadorRepository)
        {
            _equipoRepository = equipoRepository;
            _torneoRepository = torneoRepository;
            _jugadorRepository = jugadorRepository;
        }

        public async Task<IEnumerable<EquipoResponseDTO>> GetByTorneoIdAsync(Guid torneoId)
        {
            var equipos = await _equipoRepository.FindAsync(e => e.TorneoId == torneoId);
            return equipos.Select(MapToResponseDTO);
        }

        public async Task<EquipoResponseDTO> GetByIdAsync(Guid id)
        {
            var equipo = await _equipoRepository.GetByIdAsync(id);
            if (equipo == null)
                throw new NotFoundException($"No se encontró el equipo con ID {id}");

            return MapToResponseDTO(equipo);
        }

        public async Task<EquipoResponseDTO> CreateAsync(EquipoCreateDTO dto)
        {
            var torneo = await _torneoRepository.GetByIdAsync(dto.TorneoId);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {dto.TorneoId}");

            var equipo = new Equipo
            {
                Nombre = dto.Nombre,
                Logo = dto.Logo,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                TorneoId = dto.TorneoId
            };

            await _equipoRepository.AddAsync(equipo);
            return MapToResponseDTO(equipo);
        }

        public async Task<EquipoResponseDTO> CambiarEstadoAsync(Guid id, string nuevoEstadoStr)
        {
            var equipo = await _equipoRepository.GetByIdAsync(id);
            if (equipo == null)
                throw new NotFoundException($"No se encontró el equipo con ID {id}");

            if (!Enum.TryParse<EstadoInscripcion>(nuevoEstadoStr, true, out var nuevoEstado))
                throw new ValidationException($"El estado '{nuevoEstadoStr}' no es válido.");

            if (nuevoEstado == EstadoInscripcion.InscritoYConfirmado)
            {
                var torneo = await _torneoRepository.GetByIdAsync(equipo.TorneoId);
                var equiposConfirmados = await _equipoRepository.FindAsync(e => e.TorneoId == equipo.TorneoId && e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado);

                if (equiposConfirmados.Count() >= torneo!.CupoMaximo && equipo.EstadoInscripcion != EstadoInscripcion.InscritoYConfirmado)
                {
                    throw new BusinessRuleConflictException("El torneo ya ha alcanzado su cupo máximo de equipos confirmados.");
                }

                var jugadoresDelEquipo = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
                if (!jugadoresDelEquipo.Any())
                {
                    throw new BusinessRuleConflictException("El equipo no cumple con el mínimo de jugadores requeridos (Mínimo 1) para ser confirmado.");
                }
            }

            equipo.EstadoInscripcion = nuevoEstado;
            await _equipoRepository.UpdateAsync(equipo);

            return MapToResponseDTO(equipo);
        }

        private EquipoResponseDTO MapToResponseDTO(Equipo equipo)
        {
            return new EquipoResponseDTO
            {
                Id = equipo.Id,
                Nombre = equipo.Nombre,
                Logo = equipo.Logo,
                EstadoInscripcion = equipo.EstadoInscripcion.ToString(),
                TorneoId = equipo.TorneoId
            };
        }
    }
}
